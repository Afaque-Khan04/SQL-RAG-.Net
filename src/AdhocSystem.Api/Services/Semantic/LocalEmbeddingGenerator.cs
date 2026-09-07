using System.Numerics.Tensors;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace AdhocSystem.Api.Services.Semantic;

public class LocalEmbeddingGenerator : ILocalEmbeddingGenerator
{
    private static readonly Regex TokenRegex = new(@"[\w\d]+", RegexOptions.Compiled);
    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "a", "an", "and", "are", "as", "at", "be", "by", "for", "from", "has", "he", "in", "is", "it",
        "its", "of", "on", "that", "the", "to", "was", "were", "will", "with", "or", "than", "this", "these"
    };

    public float[] GenerateEmbedding(string text, int dimension = 384)
    {
        if (dimension <= 0) dimension = 384;
        var vector = new float[dimension];

        if (string.IsNullOrWhiteSpace(text))
        {
            return vector;
        }

        var normalized = text.ToLowerInvariant().Trim();
        var matches = TokenRegex.Matches(normalized);
        var termCounts = new Dictionary<string, float>(StringComparer.Ordinal);

        // 1. Extract unigrams, stems, and subwords for semantic & morphology capture
        foreach (Match match in matches)
        {
            var word = match.Value;
            if (word.Length < 2) continue;

            var weight = StopWords.Contains(word) ? 0.1f : 2.0f;
            AddWeightedTerm(termCounts, word, weight);

            // Simple suffix stemming (e.g. jackets -> jacket, cycling -> cycle, waterproof -> water)
            var stem = word;
            if (stem.EndsWith("ing") && stem.Length > 5) stem = stem.Substring(0, stem.Length - 3);
            else if (stem.EndsWith("ies") && stem.Length > 4) stem = stem.Substring(0, stem.Length - 3) + "y";
            else if (stem.EndsWith("es") && stem.Length > 4) stem = stem.Substring(0, stem.Length - 2);
            else if (stem.EndsWith("s") && stem.Length > 3) stem = stem.Substring(0, stem.Length - 1);
            else if (stem.EndsWith("ed") && stem.Length > 4) stem = stem.Substring(0, stem.Length - 2);

            if (stem != word)
            {
                AddWeightedTerm(termCounts, stem, 1.8f);
            }

            // Add char n-grams (3-grams and 4-grams) for typo/stemming resilience
            if (word.Length >= 4)
            {
                for (int i = 0; i <= word.Length - 3; i++)
                {
                    var sub3 = word.Substring(i, 3);
                    AddWeightedTerm(termCounts, sub3, 0.5f);
                }
            }
        }

        // 2. Add bigrams for contextual phrases
        var wordsList = matches.Select(m => m.Value).Where(w => w.Length >= 2 && !StopWords.Contains(w)).ToList();
        for (int i = 0; i < wordsList.Count - 1; i++)
        {
            var bigram = $"{wordsList[i]}_{wordsList[i + 1]}";
            AddWeightedTerm(termCounts, bigram, 2.0f);
        }

        // 3. Project terms into dense embedding vector via multi-hash trick
        foreach (var (term, weight) in termCounts)
        {
            var hash1 = GetStableHashCode(term, 0);
            var hash2 = GetStableHashCode(term, 1);
            var hash3 = GetStableHashCode(term, 2);

            var idx1 = Math.Abs(hash1) % dimension;
            var idx2 = Math.Abs(hash2) % dimension;
            var idx3 = Math.Abs(hash3) % dimension;

            var sign1 = (hash1 & 1) == 0 ? 1.0f : -1.0f;
            var sign2 = (hash2 & 1) == 0 ? 1.0f : -1.0f;
            var sign3 = (hash3 & 1) == 0 ? 1.0f : -1.0f;

            vector[idx1] += sign1 * weight * 1.0f;
            vector[idx2] += sign2 * weight * 0.7f;
            vector[idx3] += sign3 * weight * 0.5f;
        }

        // 4. L2 Normalize vector for cosine similarity
        NormalizeL2(vector);

        return vector;
    }

    public byte[] SerializeVector(float[] vector)
    {
        var bytes = new byte[vector.Length * sizeof(float)];
        Buffer.BlockCopy(vector, 0, bytes, 0, bytes.Length);
        return bytes;
    }

    public float[] DeserializeVector(byte[] bytes)
    {
        if (bytes == null || bytes.Length == 0) return Array.Empty<float>();
        var count = bytes.Length / sizeof(float);
        var vector = new float[count];
        Buffer.BlockCopy(bytes, 0, vector, 0, bytes.Length);
        return vector;
    }

    private static void AddWeightedTerm(Dictionary<string, float> dict, string term, float weight)
    {
        if (dict.TryGetValue(term, out var current))
        {
            dict[term] = current + weight;
        }
        else
        {
            dict[term] = weight;
        }
    }

    private static int GetStableHashCode(string str, int seed)
    {
        unchecked
        {
            int hash = 17 + seed * 31;
            foreach (char c in str)
            {
                hash = hash * 31 + c;
            }
            return hash;
        }
    }

    private static void NormalizeL2(float[] vector)
    {
        double sumSquares = 0;
        for (int i = 0; i < vector.Length; i++)
        {
            sumSquares += vector[i] * vector[i];
        }

        if (sumSquares <= 1e-12) return;

        var norm = (float)Math.Sqrt(sumSquares);
        for (int i = 0; i < vector.Length; i++)
        {
            vector[i] /= norm;
        }
    }
}
