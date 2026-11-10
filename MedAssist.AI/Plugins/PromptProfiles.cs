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
        EXAMPLE QUESTION: What is gout?

        EXAMPLE ANSWER:
        Gout is an inflammatory arthritis in which monosodium urate crystals deposit in and around the joints when the serum uric acid level stays high [1]. An attack typically begins suddenly, often at night, with severe pain, swelling and redness of a single joint, most often the first metatarsophalangeal joint [1][2]. The diagnosis is confirmed by finding needle-shaped, negatively birefringent urate crystals in fluid aspirated from the joint [2]. An acute attack is treated with a non-steroidal anti-inflammatory drug, colchicine or a corticosteroid, started as early as possible [3]. Long-term treatment with allopurinol is recommended after recurrent attacks or when tophi develop, aiming for a serum uric acid below 360 µmol/L [3]. The excerpts do not say how long allopurinol should be continued.
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
