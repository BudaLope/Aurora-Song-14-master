using System.Text.RegularExpressions;
using Content.Shared.Speech;
using Content.Server._AS.Speech.Components;
namespace Content.Server._AS.Speech.EntitySystems;

/// <summary>
/// Makes You Speak With Emphasis On Everything
/// </summary>
public sealed class EmphasisAccentSystem : EntitySystem
{
    private static readonly Regex RegexStartOfWord = new(@"([a-z])(\s?:|^)");
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<EmphasisAccentComponent, AccentGetEvent>(OnAccent);
    }
    private void OnAccent(Entity<EmphasisAccentComponent> entity, ref AccentGetEvent args)
    {
        var message = args.Message;
        message = RegexStartOfWord.Replace(message, match => match.Value.ToUpper());
        args.Message = message;
    }
}
