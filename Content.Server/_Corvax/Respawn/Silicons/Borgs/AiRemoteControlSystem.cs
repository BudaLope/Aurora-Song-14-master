using Content.Server.Radio.Components;
using Content.Server.Silicons.Laws;
using Content.Server.Chat.Managers;
using Content.Shared._Corvax.Silicons.Borgs;
using Content.Shared._Corvax.Silicons.Borgs.Components;
using Content.Shared.Actions;
using Content.Shared.Mind;
using Content.Shared.Chat;
using Content.Shared.Silicons.Laws.Components;
using Content.Shared.Silicons.StationAi;
using Content.Shared.StationAi;
using Content.Shared.Verbs;
using Robust.Server.GameObjects;
using Robust.Shared.Player;
using Robust.Shared.Map;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;

namespace Content.Server._Corvax.Silicons.Borgs;

public sealed class AiRemoteControlSystem : SharedAiRemoteControlSystem
{
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly SiliconLawSystem _lawSystem = default!;
    [Dependency] private readonly SharedStationAiSystem _stationAiSystem = default!;
    [Dependency] private readonly SharedMindSystem _mind = default!;
    [Dependency] private readonly UserInterfaceSystem _userInterface = default!;
    [Dependency] private readonly SharedTransformSystem _xformSystem = default!;
    [Dependency] private readonly IChatManager _chatManager = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    private EntityCoordinates? _coordinates;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AiRemoteControllerComponent, ReturnMindIntoAiEvent>(OnReturnMindIntoAi);
        SubscribeLocalEvent<AiRemoteControllerComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<AiRemoteControllerComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<AiRemoteControllerComponent, GetVerbsEvent<AlternativeVerb>>(OnGetVerbs);
        SubscribeLocalEvent<StationAiHeldComponent, AiRemoteControllerComponent.RemoteDeviceActionMessage>(OnUiRemoteAction);
        SubscribeLocalEvent<StationAiHeldComponent, ToggleRemoteDevicesScreenEvent>(OnToggleRemoteDevicesScreen);
    }

    private void OnMapInit(Entity<AiRemoteControllerComponent> entity, ref MapInitEvent args)
    {
        var visionComp = EnsureComp<StationAiVisionComponent>(entity.Owner);
        EntityUid? actionEnt = null;

        _actions.AddAction(entity.Owner, ref actionEnt, entity.Comp.BackToAiAction);

        if (actionEnt != null)
            entity.Comp.BackToAiActionEntity = actionEnt.Value;
    }

    private void OnShutdown(Entity<AiRemoteControllerComponent> entity, ref ComponentShutdown args)
    {
        _actions.RemoveAction(entity.Owner, entity.Comp.BackToAiActionEntity);

        var backArgs = new ReturnMindIntoAiEvent();
        backArgs.Performer = entity;

        if (TryComp(entity, out IntrinsicRadioTransmitterComponent? transmitter)
            && entity.Comp.PreviouslyTransmitterChannels != null)
            transmitter.Channels = [.. entity.Comp.PreviouslyTransmitterChannels];

        if (TryComp(entity, out ActiveRadioComponent? activeRadio)
            && entity.Comp.PreviouslyActiveRadioChannels != null)
            activeRadio.Channels = [.. entity.Comp.PreviouslyActiveRadioChannels];

        ReturnMindIntoAi(entity);
    }

    private void OnGetVerbs(Entity<AiRemoteControllerComponent> entity, ref GetVerbsEvent<AlternativeVerb> args)
    {
        var user = args.User;

        if (!TryComp<StationAiHeldComponent>(user, out var stationAiHeldComp))
            return;

        var verb = new AlternativeVerb
        {
            Text = Loc.GetString("ai-remote-control"),
            Act = () => AiTakeControl(user, entity)
        };
        args.Verbs.Add(verb);
    }

    private void OnReturnMindIntoAi(Entity<AiRemoteControllerComponent> entity, ref ReturnMindIntoAiEvent args) =>
        ReturnMindIntoAi(entity);

    public void AiTakeControl(EntityUid ai, EntityUid entity)
    {
        if (!_mind.TryGetMind(ai, out var mindId, out var mind))
            return;

        if (!TryComp<StationAiHeldComponent>(ai, out var stationAiHeldComp))
            return;

        if (!TryComp<AiRemoteControllerComponent>(entity, out var aiRemoteComp))
            return;
        if (_stationAiSystem.TryGetCore(ai, out var stationAiCore) && stationAiCore.Comp?.RemoteEntity != null
                && (Transform(stationAiCore).Coordinates.Position - Transform(entity).Coordinates.Position).Length() > 5
            )
        {
            var msg = Loc.GetString("ai-remote-out-of-range");
            var wrappedMessage = Loc.GetString("chat-manager-server-wrap-message", ("message", msg));
            _chatManager.ChatMessageToOne(ChatChannel.Server, msg, wrappedMessage, default, false, Comp<ActorComponent>(ai).PlayerSession.Channel, colorOverride: Color.FromHex("#f30707"));
            // _audio.PlayEntity(new SoundPathSpecifier("Audio/Machines/buzz-sigh.ogg"), Comp<ActorComponent>(ai).PlayerSession, stationAiCore.Comp.RemoteEntity.Value); # Todo: Figure out how to make this work
            return;
        }

        if (TryComp(entity, out IntrinsicRadioTransmitterComponent? transmitter))
        {
            aiRemoteComp.PreviouslyTransmitterChannels = [.. transmitter.Channels];

            if (TryComp(ai, out IntrinsicRadioTransmitterComponent? stationAiTransmitter))
                transmitter.Channels = [.. stationAiTransmitter.Channels];
        }

        if (TryComp(entity, out ActiveRadioComponent? activeRadio))
        {
            aiRemoteComp.PreviouslyActiveRadioChannels = [.. activeRadio.Channels];

            if (TryComp(ai, out ActiveRadioComponent? stationAiActiveRadio))
                activeRadio.Channels = [.. stationAiActiveRadio.Channels];
        }

        _mind.ControlMob(ai, entity);
        aiRemoteComp.AiHolder = ai;
        aiRemoteComp.LinkedMind = mindId;

        stationAiHeldComp.CurrentConnectedEntity = entity;

        _stationAiSystem.SwitchRemoteEntityMode(stationAiCore, false);

        RewriteLaws(ai, entity);
    }

    private void OnToggleRemoteDevicesScreen(EntityUid uid, StationAiHeldComponent component, ToggleRemoteDevicesScreenEvent args)
    {
        if (args.Handled || !TryComp<ActorComponent>(uid, out var actor))
            return;
        args.Handled = true;

        _userInterface.TryToggleUi(uid, RemoteDeviceUiKey.Key, actor.PlayerSession);

        var query = EntityManager.EntityQueryEnumerator<AiRemoteControllerComponent>();
        var remoteDevices = new List<RemoteDevicesData>();

        while (query.MoveNext(out var queryUid, out var comp))
        {
            var data = new RemoteDevicesData
            {
                NetEntityUid = GetNetEntity(queryUid),
                DisplayName = Comp<MetaDataComponent>(queryUid).EntityName,
                DevicePosX = Transform(queryUid).Coordinates.X,
                DevicePosY = Transform(queryUid).Coordinates.Y
            };
            if (_stationAiSystem.TryGetCore(uid, out var stationAiCore) && stationAiCore.Comp?.RemoteEntity != null
                    && (Transform(stationAiCore).Coordinates.Position - Transform(queryUid).Coordinates.Position).Length() < 3000
                )
            {
                remoteDevices.Add(data);
            };
        }

        var state = new RemoteDevicesBuiState(remoteDevices);
        _userInterface.SetUiState(uid, RemoteDeviceUiKey.Key, state);
    }

    private void OnUiRemoteAction(EntityUid uid, StationAiHeldComponent component, AiRemoteControllerComponent.RemoteDeviceActionMessage msg)
    {
        if (msg.RemoteAction == null)
            return;

        var target = GetEntity(msg.RemoteAction?.Target);

        if (!HasComp<AiRemoteControllerComponent>(target))
            return;

        switch (msg.RemoteAction?.ActionType)
        {
            case RemoteDeviceActionEvent.RemoteDeviceActionType.MoveToDevice:
                if (!_stationAiSystem.TryGetCore(uid, out var stationAiCore)
                    || stationAiCore.Comp?.RemoteEntity == null)
                    return;
                _xformSystem.SetCoordinates(stationAiCore.Comp.RemoteEntity.Value, Transform(target.Value).Coordinates);
                break;

            case RemoteDeviceActionEvent.RemoteDeviceActionType.TakeControl:
                AiTakeControl(uid, target.Value);
                break;
        }
    }

    private void RewriteLaws(EntityUid from, EntityUid to)
    {
        if (!TryComp<SiliconLawProviderComponent>(from, out var fromLawsComp))
            return;

        if (!TryComp<SiliconLawProviderComponent>(to, out var toLawsComp))
            return;

        if (fromLawsComp.Lawset == null)
            return;

        var fromLaws = _lawSystem.GetLaws(from);
        _lawSystem.SetLawsSilent(fromLaws.Laws, to);
    }
}