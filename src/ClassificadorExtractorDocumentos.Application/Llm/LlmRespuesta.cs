namespace ClassificadorExtractorDocumentos.Application.Llm;

/// <summary>Utilidades para tratar respuestas de LLM: los modelos a veces envuelven el JSON
/// en fences de markdown o prosa; aquí se localiza el primer objeto JSON balanceado.</summary>
public static class LlmRespuesta
{
    public static string? ExtraerPrimerObjetoJson(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
        {
            return null;
        }

        var inicio = texto.IndexOf('{');
        if (inicio < 0)
        {
            return null;
        }

        var profundidad = 0;
        var enString = false;
        var escapado = false;
        for (var i = inicio; i < texto.Length; i++)
        {
            var c = texto[i];
            if (escapado) { escapado = false; continue; }
            if (c == '\\' && enString) { escapado = true; continue; }
            if (c == '"') { enString = !enString; continue; }
            if (enString) { continue; }
            if (c == '{') { profundidad++; }
            else if (c == '}')
            {
                profundidad--;
                if (profundidad == 0)
                {
                    return texto[inicio..(i + 1)];
                }
            }
        }

        return null;
    }
}
