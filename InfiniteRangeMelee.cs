using System;
using System.IO;
using System.Text;
using System.Text.Json;

namespace DungeonRampageCheat
{
    public class InfiniteRangeMelee
    {
        private string gamePath = "";

        public bool SetGamePath(string path)
        {
            if (Directory.Exists(path))
            {
                string testFile = Path.Combine(path, "Resources", "Combat", "AttackTimeline.json");
                if (File.Exists(testFile))
                {
                    gamePath = path;
                    return true;
                }
            }
            return false;
        }

        public int ApplyInfiniteRange(double multiplier)
        {
            if (string.IsNullOrEmpty(gamePath))
                return -1;

            int total = 0;

            string mainFile = Path.Combine(gamePath, "Resources", "Combat", "AttackTimeline.json");
            if (File.Exists(mainFile))
            {
                total += ModifyRadius(mainFile, multiplier);
            }

            total += ModifyCharacterRadius("Berserker", multiplier);
            total += ModifyCharacterRadius("ranger", multiplier);
            total += ModifyCharacterRadius("wizard", multiplier);
            total += ModifyCharacterRadius("chef", multiplier);

            return total;
        }

        public int RestoreOriginal()
        {
            if (string.IsNullOrEmpty(gamePath))
                return -1;

            int restored = 0;

            string mainFile = Path.Combine(gamePath, "Resources", "Combat", "AttackTimeline.json");
            string mainBackup = mainFile + ".backup_original";
            if (File.Exists(mainBackup))
            {
                File.Copy(mainBackup, mainFile, true);
                restored++;
            }

            restored += RestoreCharacterRadius("Berserker");
            restored += RestoreCharacterRadius("ranger");
            restored += RestoreCharacterRadius("wizard");
            restored += RestoreCharacterRadius("chef");

            return restored;
        }

        private int ModifyCharacterRadius(string dirName, double multiplier)
        {
            string charDir = Path.Combine(gamePath, "Resources", "Art3D", "Avatar", dirName);
            int total = 0;

            if (Directory.Exists(charDir))
            {
                foreach (var file in Directory.GetFiles(charDir, "db_time_*.json"))
                {
                    total += ModifyRadius(file, multiplier);
                }
            }

            return total;
        }

        private int RestoreCharacterRadius(string dirName)
        {
            string charDir = Path.Combine(gamePath, "Resources", "Art3D", "Avatar", dirName);
            int restored = 0;

            if (Directory.Exists(charDir))
            {
                foreach (var file in Directory.GetFiles(charDir, "db_time_*.json"))
                {
                    string backupPath = file + ".backup_original";
                    if (File.Exists(backupPath))
                    {
                        File.Copy(backupPath, file, true);
                        restored++;
                    }
                }
            }

            return restored;
        }

        private int ModifyRadius(string filePath, double multiplier)
        {
            try
            {
                string backupPath = filePath + ".backup_original";
                if (!File.Exists(backupPath))
                    File.Copy(filePath, backupPath, true);

                string jsonText = File.ReadAllText(filePath);

                using JsonDocument doc = JsonDocument.Parse(jsonText);
                using MemoryStream stream = new();
                using Utf8JsonWriter writer = new(stream, new JsonWriterOptions { Indented = false });
                int count = ProcessRadiusRecursive(writer, doc.RootElement, multiplier);
                writer.Flush();

                string modifiedJson = Encoding.UTF8.GetString(stream.ToArray());
                File.WriteAllText(filePath, modifiedJson, Encoding.UTF8);
                return count;
            }
            catch
            {
                return 0;
            }
        }

        private int ProcessRadiusRecursive(Utf8JsonWriter writer, JsonElement element, double multiplier)
        {
            int count = 0;

            switch (element.ValueKind)
            {
                case JsonValueKind.Object:
                    writer.WriteStartObject();

                    bool isCircleCollider = false;
                    foreach (var prop in element.EnumerateObject())
                    {
                        if (prop.Name == "type" && prop.Value.ValueKind == JsonValueKind.String &&
                            prop.Value.GetString() == "circleCollider")
                        {
                            isCircleCollider = true;
                            break;
                        }
                    }

                    foreach (var prop in element.EnumerateObject())
                    {
                        writer.WritePropertyName(prop.Name);

                        if (isCircleCollider && prop.Name == "radius" && prop.Value.ValueKind == JsonValueKind.Number)
                        {
                            writer.WriteNumberValue(prop.Value.GetDouble() * multiplier);
                            count++;
                        }
                        else
                        {
                            count += ProcessRadiusRecursive(writer, prop.Value, multiplier);
                        }
                    }

                    writer.WriteEndObject();
                    break;

                case JsonValueKind.Array:
                    writer.WriteStartArray();
                    foreach (var item in element.EnumerateArray())
                        count += ProcessRadiusRecursive(writer, item, multiplier);
                    writer.WriteEndArray();
                    break;

                default:
                    WriteJsonValue(writer, element);
                    break;
            }

            return count;
        }

        private void WriteJsonValue(Utf8JsonWriter writer, JsonElement element)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.String:
                    writer.WriteStringValue(element.GetString());
                    break;
                case JsonValueKind.Number:
                    if (element.TryGetInt32(out int intValue))
                        writer.WriteNumberValue(intValue);
                    else if (element.TryGetInt64(out long longValue))
                        writer.WriteNumberValue(longValue);
                    else
                        writer.WriteNumberValue(element.GetDouble());
                    break;
                case JsonValueKind.True:
                    writer.WriteBooleanValue(true);
                    break;
                case JsonValueKind.False:
                    writer.WriteBooleanValue(false);
                    break;
                case JsonValueKind.Null:
                    writer.WriteNullValue();
                    break;
            }
        }
    }
}