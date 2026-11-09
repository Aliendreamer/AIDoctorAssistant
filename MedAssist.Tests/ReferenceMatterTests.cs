using MedAssist.AI.Plugins;
using Xunit;

namespace MedAssist.Tests;

public class ReferenceMatterTests
{
    [Fact]
    public void NumberedReferenceList_IsReferenceMatter()
    {
        const string text = """
            - **1. Найденов А. фебрилни гърчове, Педиатрия /С/, 1992, XXXI, кн 2, стр. 2.**
            - **2. De Lorenzo R., Febrile Convulsions, in: Neurology in Clinical Practice, Eds W. Bradley et al - Boston, Butterwarths-Heineman, 1991, p. 1468.**
            - **3. Donohie 0. Febrile Convulsions, in: Epilepsy Syndromes, Eds J. Reger et al., London & Paris, John Libley Eurotext, 1985, p. 37.**
            """;

        Assert.True(ReferenceMatter.IsReferenceMatter(text));
    }

    [Fact]
    public void UnnumberedReadingList_IsReferenceMatter()
    {
        const string text = """
            Goldman A, Hain R, Lieben S: The Oxford Textbook of Palliative Care in Children, ed 2, Oxford, 2011, Oxford University Press.

            Brook C, Clayton P, Brown R: Brook's Clinical Paediatric Endocrinology, ed 6, Oxford, 2009, Wiley-Blackwell.

            British Dietetic Association: Available at: http://www.bda.uk.com.
            """;

        Assert.True(ReferenceMatter.IsReferenceMatter(text));
    }

    [Fact]
    public void IndexPageWithOneEntryPerLine_IsReferenceMatter()
    {
        const string text = """
            Червен очен рефлекс, 160, 164

            Черен дроб, плътен, 18

            Черепно мозъчни нерви (ЧВН), 19

            Чернодробна дисфункция, 375

            Чернодробна недостатъчност (фулминантен хепатит), 93, 380

            Хематурия, 357-359
            """;

        Assert.True(ReferenceMatter.IsReferenceMatter(text));
    }

    [Fact]
    public void RunOnIndexPage_IsReferenceMatter()
    {
        const string text =
            "Абстиненция 392 Абсцес парафарингеален 250 периапендикуларен 574 перитонзиларен 250 ретрофарингеален 250 " +
            "субфреничен 287 Аденоидит 248 Аденовирусна инфекция 312 Акне 466 Алергичен ринит 249 Алкохолно отравяне 402 " +
            "Анафилаксия 95 Анемия 437 Ангиоедем 99 Анорексия 384 Апендицит 574 Апнея 212 Аритмии 316 Артрит 497 " +
            "Асцит 151 Атаксия 343 Атопичен дерматит 461 Аутизъм 387 Ацидоза 120";

        Assert.True(ReferenceMatter.IsReferenceMatter(text));
    }

    [Fact]
    public void TextThatOpensWithOneCitation_IsKept()
    {
        const string text = """
            1. Горанов Ив. - Патология на съединителната тъкан, в кн: Ревматология Ред Ив. Цончев, София, Мед. и физкултура, 1962, с. 16.
            При заболяванията на опорно-двигателния апарат най-често субективно оплакване е болката и то ставната болка. Поради това анамнезата е от решаващо значение за диагнозата.

            а/ кога и как е започнала болката и какво е нейното протичане? Изяснява се: възрастта, в която е започнала болката, връзката със сезона и промени в климата; по кое време на денонощието се явява.
            """;

        Assert.False(ReferenceMatter.IsReferenceMatter(text));
    }

    [Fact]
    public void LongTextWithAFewCitationsAfterIt_IsKept()
    {
        const string text = """
            **Хипоталамусът е част от диенцефалона. Разположен е в дъното на третия вентрикул. В процеса на ембрионалното развитие той се формира рано и регулира функциите на хипофизата чрез освобождаващи и потискащи хормони, които достигат до нея по порталната система.**
            **Диаметърът на хипоталамуса е около 25 мм и тежи 4 г. Той изпълнява следните функции: регулира телесната температура, глада и жаждата, съня и будността, както и поведенческите реакции при стрес.**
            1. Brook C. Clinical Paediatric Endocrinology, Oxford, 1995.
            2. Wilson J. et al. Williams Textbook of Endocrinology, Philadelphia, 1998.
            """;

        Assert.False(ReferenceMatter.IsReferenceMatter(text));
    }

    [Fact]
    public void ClinicalTextWithDosesAndAges_IsKept()
    {
        const string text =
            "Като фебрилен се означава гърчът у дете на възраст между 6 и 48 месеца, който възниква при температура над 38.5 °С. " +
            "Диазепам се прилага ректално в доза 0.5 mg/kg, а при продължаване на гърча след 10 мин. дозата се повтаря. " +
            "Парацетамол 15 mg/kg на 6 часа или ибупрофен 10 mg/kg на 8 часа овладяват температурата. Рецидив има при 30 % от децата.";

        Assert.False(ReferenceMatter.IsReferenceMatter(text));
    }

    [Fact]
    public void TableOfValues_IsKept()
    {
        const string text = """
            | Показател | 1 год | 5 год | 10 год |
            | --- | --- | --- | --- |
            | Пулс | 120 | 100 | 85 |
            | Дишане | 30 | 24 | 20 |
            | Систолно налягане | 90 | 95 | 105 |
            """;

        Assert.False(ReferenceMatter.IsReferenceMatter(text));
    }
}
