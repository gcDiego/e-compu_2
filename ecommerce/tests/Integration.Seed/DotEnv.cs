// Lee el .env de la raíz, el único archivo de configuración local del repo.
// Los servicios lo reciben como variables de entorno desde start.sh; estas
// herramientas se ejecutan sueltas, así que lo leen directamente.
public static class DotEnv
{
    public static Dictionary<string, string?> Read(string path)
    {
        var values = new Dictionary<string, string?>();
        if (!File.Exists(path)) return values;

        foreach (var line in File.ReadLines(path))
        {
            var text = line.Trim();
            if (text.Length == 0 || text.StartsWith('#')) continue;

            var separator = text.IndexOf('=');
            if (separator <= 0) continue;

            var key = text[..separator].Trim();
            var value = text[(separator + 1)..].Trim();
            if (value.Length > 1 && (value[0] == '\'' || value[0] == '"') && value[^1] == value[0])
                value = value[1..^1];

            // La configuración de .NET usa ':'; las variables de entorno usan '__'.
            values[key.Replace("__", ":")] = value;
        }

        return values;
    }
}
