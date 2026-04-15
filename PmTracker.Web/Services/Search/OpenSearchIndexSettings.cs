namespace PmTracker.Web.Services.Search;

internal static class OpenSearchIndexSettings
{
    public static string Build(int embeddingDimensions, bool embeddingsEnabled)
    {
        var embeddingMapping = embeddingsEnabled
            ? $"\"embedding\": {{ \"type\": \"knn_vector\", \"dimension\": {embeddingDimensions} }},"
            : string.Empty;

        return "{" +
            "\"settings\": {" +
                "\"index\": { \"knn\": " + (embeddingsEnabled ? "true" : "false") + " }," +
                "\"analysis\": {" +
                    "\"analyzer\": {" +
                        "\"czech_custom\": {" +
                            "\"type\": \"custom\"," +
                            "\"tokenizer\": \"standard\"," +
                            "\"filter\": [\"lowercase\", \"asciifolding\", \"czech_stop\"]" +
                        "}" +
                    "}," +
                    "\"filter\": {" +
                        "\"czech_stop\": { \"type\": \"stop\", \"stopwords\": \"_czech_\" }" +
                    "}" +
                "}" +
            "}," +
            "\"mappings\": {" +
                "\"properties\": {" +
                    "\"entity_type\": { \"type\": \"keyword\" }," +
                    "\"entity_id\": { \"type\": \"keyword\" }," +
                    "\"projekt_id\": { \"type\": \"integer\" }," +
                    "\"title\": { \"type\": \"text\", \"analyzer\": \"czech_custom\" }," +
                    "\"body\": { \"type\": \"text\", \"analyzer\": \"czech_custom\" }," +
                    "\"keywords\": { \"type\": \"keyword\" }," +
                    embeddingMapping +
                    "\"updated_at\": { \"type\": \"date\" }," +
                    "\"meta\": { \"type\": \"object\", \"enabled\": false }" +
                "}" +
            "}" +
        "}";
    }
}
