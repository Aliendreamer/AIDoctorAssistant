using MedAssist.AI.Plugins;
using Xunit;

namespace MedAssist.Tests;

public class ConditionNameGuardTests
{
    // Near neighbours of the invented names, all found in the books.
    private static readonly string[] _vocabulary =
        ["halman", "holman", "wolman", "starling", "older", "central", "raven", "zaritsky", "desmond", "дори"];

    private static bool Passes(string question, string excerpt) =>
        ConditionNameGuard.AllAppearIn(ConditionNameGuard.ExtractNames(question), [excerpt]);

    [Theory]
    [InlineData("Какво е синдром на Зарковски-Хелман?", "зарковски", "хелман")]
    [InlineData("какво е синдром на зарковски?", "зарковски")]
    [InlineData("Симптоми на болестта на Кавазаки при деца", "кавазаки")]
    [InlineData("Лечение на синдрома на Chiari", "chiari")]
    [InlineData("What is the recommended treatment for Varnholt-Mercer syndrome in children?", "varnholt", "mercer")]
    [InlineData("Bramwell-Kraven disease in toddlers", "bramwell", "kraven")]
    [InlineData("What is Crohn's disease?", "crohn")]
    public void ExtractNames_FindsTheNamedCondition(string question, params string[] parts)
    {
        var name = Assert.Single(ConditionNameGuard.ExtractNames(question));

        Assert.Equal(parts, name);
    }

    [Theory]
    [InlineData("Лечение на остър среден отит при деца")]
    [InlineData("Дете с висока температура и петехиален обрив")]
    [InlineData("Bronchiolitis in infants")]
    [InlineData("What is celiac disease?")]
    [InlineData("Celiac disease in children")]
    [InlineData("heart disease in children")]
    [InlineData("А при кърмачета?")]
    public void ExtractNames_IgnoresQuestionsWithoutAName(string question)
    {
        Assert.Empty(ConditionNameGuard.ExtractNames(question));
    }

    [Theory]
    [InlineData("Какво е болест на Кавазаки?", "Системен васкулит. Болест на Kawasaki се среща при деца.")]
    [InlineData("Какво е болест на Гоше?", "Болест на Gaucher е лизозомна болест.")]
    [InlineData("Какво е синдром на Даун?", "Синдром на Down е тризомия 21.")]
    [InlineData("Какво е болест на Уилсън?", "Болест на Wilson - натрупване на мед.")]
    [InlineData("Какво е синдром на Шегрен?", "Синдром на Sjögren засяга жлезите.")]
    [InlineData("Какво е пурпура и болест на Шьонлайн-Хенох?", "Пурпура на Schönlein-Henoch е васкулит.")]
    [InlineData("Какво е синдром на Гилен-Баре?", "Синдром на Guillain-Barre е полиневропатия.")]
    [InlineData("What is Kawasaki disease?", "Болест на Кавазаки се среща при деца.")]
    [InlineData("Какво е синдром на раздразненото черво?", "При раздразнено черво се наблюдава болка.")]
    public void RealNames_MatchAcrossScriptsAndSpellings(string question, string excerpt)
    {
        Assert.True(Passes(question, excerpt));
    }

    [Theory]
    [InlineData("Какво е синдром на Зарковски-Хелман?", "Синдром на Alagille е холестаза с дори редки находки.")]
    [InlineData("What is the treatment for Varnholt-Mercer syndrome?", "Тромбоцитна дисфункция, лечение и мерки при кръвен месец.")]
    [InlineData("Какво е болест на Дорвик?", "Често дори при деца има кашлица.")]
    public void InventedNames_AreNotFoundInUnrelatedExcerpts(string question, string excerpt)
    {
        Assert.False(Passes(question, excerpt));
    }

    [Theory]
    [InlineData("Гудпасчър", "goodpasture")]
    [InlineData("Гоше", "gaucher")]
    [InlineData("Уилсън", "wilson")]
    [InlineData("Търнър", "turner")]
    [InlineData("Кавазаки", "kawasaki")]
    [InlineData("Даун", "down")]
    [InlineData("Кушинг", "cushing")]
    [InlineData("Хенох", "henoch")]
    [InlineData("Guillain", "guillain")]
    public void FindBookSpellings_ResolvesNamesToTheSpellingTheBooksUse(string part, string expected)
    {
        var found = ConditionNameGuard.FindBookSpellings([[part.ToLowerInvariant()]], [.. _vocabulary, expected]);

        Assert.Equal([expected], found);
    }

    [Theory]
    [InlineData("зарковски")]
    [InlineData("хелман")]
    [InlineData("варнхолт")]
    [InlineData("дорвик")]
    [InlineData("остерлинг")]
    public void FindBookSpellings_NeverTurnsAnInventedNameIntoARealOne(string part)
    {
        Assert.Empty(ConditionNameGuard.FindBookSpellings([[part]], _vocabulary));
    }

    [Fact]
    public void FindBookSpellings_IgnoresCyrillicWordsInTheVocabulary()
    {
        Assert.Empty(ConditionNameGuard.FindBookSpellings([["кавазаки"]], ["кавазаки"]));
    }

    [Fact]
    public void EveryNamedCondition_MustAppear()
    {
        var names = ConditionNameGuard.ExtractNames("Разлика между болест на Кавазаки и синдром на Зарковски");

        Assert.Equal(2, names.Count);
        Assert.False(ConditionNameGuard.AllAppearIn(names, ["Болест на Kawasaki."]));
        Assert.True(ConditionNameGuard.AllAppearIn(names, ["Болест на Kawasaki.", "Описан от Зарковски."]));
    }

    [Fact]
    public void QuestionWithoutAName_AlwaysPasses()
    {
        Assert.True(Passes("Лечение на остър среден отит при деца", "Нещо напълно различно."));
    }
}
