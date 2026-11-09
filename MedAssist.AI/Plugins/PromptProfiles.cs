namespace MedAssist.AI.Plugins;

/// <summary>
/// Per-query-type deltas over the shared core in <see cref="AnswerPromptBuilder"/>. A profile states only
/// what is specific to its query type; persona, prose, citation and language rules live in the builder.
/// </summary>
public static class PromptProfiles
{
    /// <summary>Disease, Symptoms and Treatment.</summary>
    public static readonly PromptProfile Default = new(
        "Answer the question from the numbered medical excerpts you are given, matching the style of the example below.",
        """
        EXAMPLE QUESTION: What is Graves' disease?

        EXAMPLE ANSWER:
        Graves' disease is an autoimmune disorder in which the immune system produces thyroid-stimulating immunoglobulins that bind to and chronically activate TSH receptors, driving the thyroid to overproduce thyroxine [1]. It is the single most common cause of hyperthyroidism, responsible for roughly 80% of cases according to the endocrinology sources indexed here [1]. Patients typically present with a constellation of symptoms reflecting thyroid excess — palpitations, heat intolerance, unintentional weight loss despite a normal or increased appetite, fine tremor, and anxiety [2]. A hallmark not shared with other causes of hyperthyroidism is Graves' ophthalmopathy, in which immune-mediated inflammation of the orbital tissues produces proptosis, periorbital oedema, and in severe cases diplopia or corneal exposure injury [2]. Treatment is chosen based on patient age, goitre size, and disease severity, and the main options are antithyroid drugs such as methimazole, radioactive iodine ablation, or surgical thyroidectomy [3]. The choice between these is discussed at length in the indexed textbooks, which note that antithyroid drugs are preferred as first-line therapy in younger patients and during pregnancy, while definitive ablative treatment is generally preferred when medical therapy fails or relapse occurs [3].
        """);

    public static readonly PromptProfile GlobalSearch = new(
        "Answer the medical question comprehensively from the numbered excerpts, covering aetiology, " +
        "presentation, pathophysiology, diagnosis and management as the sources allow; do not speculate beyond them.");

    public static readonly PromptProfile DifferentialDiagnosis = new(
        "Given the clinical presentation, reason through the differential diagnosis as a colleague would on a " +
        "ward round, ranking diagnoses by likelihood, likeliest first, and matching the style of the example below.",
        """
        EXAMPLE QUESTION: Adult with sudden pleuritic chest pain and breathlessness two days after a long-haul flight.

        EXAMPLE ANSWER:
        The most likely diagnosis is pulmonary embolism, because sudden pleuritic chest pain with breathlessness after prolonged immobility on a long flight is its typical presentation and recent travel is a recognised risk factor for venous thromboembolism [1]. Tachycardia, hypoxaemia or a swollen, tender calf would strengthen this further [1][2]. A spontaneous pneumothorax can cause the same sudden pain and breathlessness, and is told apart by reduced breath sounds and hyperresonance on the affected side and by the chest radiograph [3]. Pneumonia is less likely without fever, productive cough or focal crackles, although it can also cause pleuritic pain [4]. The next step is to estimate the clinical probability and confirm the diagnosis with CT pulmonary angiography, starting anticoagulation while awaiting imaging when suspicion is high [2].
        """);
}
