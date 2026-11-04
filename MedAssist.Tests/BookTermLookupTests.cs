using MedAssist.AI.Plugins;
using Xunit;

namespace MedAssist.Tests;

public class BookTermLookupTests
{
    // Lower weight means the word is more common in the books.
    private static readonly Dictionary<string, float> _weights = new()
    {
        ["бронхиолит"] = 5f, ["бронхиолита"] = 6f, ["бронхиолитът"] = 7f, ["бронхиолитен"] = 8f,
        ["отит"] = 5f, ["стридор"] = 5f, ["парацетамол"] = 5f, ["конюнктивална"] = 5f,
        ["пневмония"] = 3f, ["левкемия"] = 4f, ["диабет"] = 4f, ["апендицит"] = 5f,
        ["гастроентерит"] = 5f, ["антибиотици"] = 3f, ["хипертензия"] = 5f, ["хипотиреоидизъм"] = 6f,
        ["стрептокок"] = 5f, ["конюнктивит"] = 5f,
        ["инфантилна"] = 6f, ["генерализирана"] = 5f, ["синдром"] = 2f, ["симптоми"] = 2f, ["хоспитализации"] = 4f, ["кръвен"] = 5f, ["северна"] = 8f, ["кавазаки"] = 5f,
        ["bronchiolitis"] = 5f, ["treatment"] = 4f,
    };

    private static IReadOnlyList<string> Find(string question) =>
        BookTermLookup.Find(question, ConditionNameGuard.ExtractNames(question), _weights);

    [Theory]
    [InlineData("Treatment of acute otitis media in children", "отит")]
    [InlineData("barking cough and inspiratory stridor", "стридор")]
    [InlineData("Management of paracetamol poisoning in a child", "парацетамол")]
    [InlineData("rash and conjunctival injection", "конюнктивална")]
    [InlineData("severe pneumonia", "пневмония")]
    [InlineData("acute leukaemia", "левкемия")]
    [InlineData("type 1 diabetes", "диабет")]
    [InlineData("signs of appendicitis", "апендицит")]
    [InlineData("viral gastroenteritis", "гастроентерит")]
    [InlineData("which antibiotics", "антибиотици")]
    [InlineData("portal hypertension", "хипертензия")]
    [InlineData("congenital hypothyroidism", "хипотиреоидизъм")]
    [InlineData("group A streptococcus", "стрептокок")]
    [InlineData("allergic conjunctivitis", "конюнктивит")]
    public void Find_ReturnsTheBooksBulgarianTerm(string question, string expected) =>
        Assert.Contains(expected, Find(question));

    [Fact]
    public void Find_KeepsOnlyTheCommonestFormsOfAWord() =>
        Assert.Equal(["бронхиолит", "бронхиолита"], Find("Bronchiolitis in infants"));

    [Theory]
    [InlineData("Bramwell-Kraven disease in toddlers")]
    [InlineData("What is Kawasaki disease?")]
    public void Find_LeavesNamedConditionsToTheNameLookup(string question) => Assert.Empty(Find(question));

    [Theory]
    [InlineData("severe headache")]
    [InlineData("general weakness")]
    [InlineData("newborn infants")]
    [InlineData("symptoms of a rare syndrome")]
    [InlineData("hospital care")]
    [InlineData("What is the paediatric dose of fenarex?")]
    public void Find_IgnoresShortWordsFalseFriendsAndUnknownWords(string question) => Assert.Empty(Find(question));

    [Fact]
    public void Find_LeavesBulgarianQuestionsAlone() =>
        Assert.Empty(Find("Лечение на bronchiolitis при кърмачета"));
}
