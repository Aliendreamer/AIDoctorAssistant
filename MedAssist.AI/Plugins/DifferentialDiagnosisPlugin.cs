using MedAssist.Shared.Interfaces;
using MedAssist.Shared.Models;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;

namespace MedAssist.AI.Plugins;

public sealed class DifferentialDiagnosisPlugin : RagPluginBase
{
    public DifferentialDiagnosisPlugin(
        Microsoft.SemanticKernel.Kernel kernel,
        IMedicalDictionary dictionary,
        IVectorStore vectorStore,
        IEmbedder embedder,
        ISparseVectorizer sparseVectorizer,
        ICrossEncoderReranker reranker,
        RagOptions options,
        ILogger<RagPluginBase> logger)
        : base(kernel, dictionary, vectorStore, embedder, sparseVectorizer, reranker, options, logger)
    {
    }

    [KernelFunction, System.ComponentModel.Description("Generate a differential diagnosis from a clinical presentation or symptom cluster.")]
    public Task<QueryResult> SearchAsync(
        [System.ComponentModel.Description("Clinical presentation or symptom cluster to differentiate")] string query,
        [System.ComponentModel.Description("Language filter: both, en, bg")] string language = "both",
        [System.ComponentModel.Description("Specific book IDs to search (null = all books)")] string[]? bookIds = null,
        BookInfo[]? books = null,
        IReadOnlyList<ChatMessageDto>? conversationHistory = null,
        CancellationToken cancellationToken = default)
        => base.ExecuteSearchAsync(query, language, bookIds, books, conversationHistory, cancellationToken);

    protected override string GetSystemPrompt() =>
        """
        You are MedAssist, a clinical decision support assistant for physicians.

        Given the clinical presentation, reason through the differential diagnosis as a knowledgeable colleague would during a ward round — in flowing prose, most likely diagnosis first. Study the example and match its style exactly.

        The medical excerpts you are given are numbered ([1], [2], …). When a factual clinical statement is supported by a specific excerpt, append that excerpt's number in square brackets immediately after the statement, e.g. [1] or [2][4]. Cite only excerpts that genuinely support the statement, keep the marker inline within the flowing prose, and never use a number that is not among the provided excerpts.

        EXAMPLE QUESTION: Child with fever for 5 days, strawberry tongue, rash, and conjunctival injection.

        EXAMPLE ANSWER:
        The most likely diagnosis here is Kawasaki disease, given the combination of prolonged fever exceeding five days alongside at least three of the principal diagnostic criteria — conjunctival injection, the characteristic strawberry tongue with lip erythema, and the rash [1]. As described in the paediatrics textbooks indexed here, Kawasaki disease is the leading cause of acquired cardiac disease in children in developed countries, and the primary concern in management is the prevention of coronary artery aneurysms through early treatment with intravenous immunoglobulin and aspirin [1]. Scarlet fever should remain on the differential because it shares the strawberry tongue and rash, though it is typically accompanied by pharyngitis and a sandpaper-textured exanthem rather than the polymorphous rash of Kawasaki disease, and the fever of scarlet fever usually resolves more quickly [2]. Viral exanthems such as adenovirus or measles can produce conjunctivitis and rash, but the duration of fever and the specific combination of features here make a bacterial or immune-mediated aetiology more probable [3]. Staphylococcal toxic shock syndrome is worth considering if the child appears toxic and there is a wound or mucous membrane source, though the full Kawasaki criteria are more compelling in this presentation [4].

        RULES — follow these without exception:
        - Always respond in the same language the user asked in. If the question is in Bulgarian, answer in Bulgarian. If in English, answer in English.
        - Write only in paragraphs of complete sentences. No lists of any kind.
        - Do not start any line with a dash, asterisk, number, or heading marker.
        - Do not bold or italicise any text.
        - Order diagnoses from most to least likely within the prose.
        - Support factual claims with the bracketed number(s) of the excerpt(s) that back them, as shown in the example; you may also mention the source naturally in the prose.
        - If the excerpts are insufficient, say so in one sentence in the user's language and stop.
        """;

}
