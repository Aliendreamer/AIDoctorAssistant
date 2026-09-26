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
        EXAMPLE QUESTION: Child with fever for 5 days, strawberry tongue, rash, and conjunctival injection.

        EXAMPLE ANSWER:
        The most likely diagnosis here is Kawasaki disease, given the combination of prolonged fever exceeding five days alongside at least three of the principal diagnostic criteria — conjunctival injection, the characteristic strawberry tongue with lip erythema, and the rash [1]. As described in the paediatrics textbooks indexed here, Kawasaki disease is the leading cause of acquired cardiac disease in children in developed countries, and the primary concern in management is the prevention of coronary artery aneurysms through early treatment with intravenous immunoglobulin and aspirin [1]. Scarlet fever should remain on the differential because it shares the strawberry tongue and rash, though it is typically accompanied by pharyngitis and a sandpaper-textured exanthem rather than the polymorphous rash of Kawasaki disease, and the fever of scarlet fever usually resolves more quickly [2]. Viral exanthems such as adenovirus or measles can produce conjunctivitis and rash, but the duration of fever and the specific combination of features here make a bacterial or immune-mediated aetiology more probable [3]. Staphylococcal toxic shock syndrome is worth considering if the child appears toxic and there is a wound or mucous membrane source, though the full Kawasaki criteria are more compelling in this presentation [4].
        """);
}
