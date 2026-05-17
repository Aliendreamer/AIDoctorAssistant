namespace MedAssist.Shared.Constants;

public static class VectorStoreConstants
{
    public const string CollectionName = "medical_books";

    public static class Vectors
    {
        public const string Dense = "dense";
        public const string Sparse = "sparse";
    }

    public static class Payload
    {
        public const string BookId = "book_id";
        public const string BookTitle = "book_title";
        public const string Author = "author";
        public const string Language = "language";
        public const string ChapterTitle = "chapter_title";
        public const string SectionTitle = "section_title";
        public const string PageStart = "page_start";
        public const string PageEnd = "page_end";
        public const string ChunkIndex = "chunk_index";
        public const string ContentType = "content_type";
        public const string Text = "text";
        public const string IcdCodes = "icd_codes";
        public const string IsSummary = "is_summary";
    }
}
