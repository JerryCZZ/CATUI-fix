using System;
using System.IO;
using System.Reflection;

internal static class CatuiGearsPreviewResources
{
    private static readonly string[] Names =
    {
        "BooksOff.png",
        "BooksOn.png",
        "BossVar.png",
        "BossVarOff.png",
        "BuffVar.png",
        "BuffVarOff.png",
        "ChargedVar.png",
        "ChargedVarOff.png",
        "FeralVar.png",
        "FeralVarOff.png",
        "InfernalVar.png",
        "InfernalVarOff.png",
        "NormalVar.png",
        "NormalVarOff.png",
        "RadiatedVar.png",
        "RadiatedVarOff.png",
    };

    internal static void Restore(Assembly assembly)
    {
        try
        {
            string root = Path.GetDirectoryName(assembly.Location);
            if (string.IsNullOrEmpty(root))
                return;

            string destination = Path.Combine(root, "Misc", "img");
            Directory.CreateDirectory(destination);

            foreach (string name in Names)
            {
                string resourceName = "CATUI.GearsPreview." + name;
                using (Stream input = assembly.GetManifestResourceStream(resourceName))
                {
                    if (input == null)
                    {
                        Log.Warning("[CATUI] Missing Gears preview resource: " + name);
                        continue;
                    }

                    string file = Path.Combine(destination, name);
                    using (MemoryStream buffer = new MemoryStream())
                    {
                        input.CopyTo(buffer);
                        byte[] data = buffer.ToArray();
                        bool write = true;
                        if (File.Exists(file))
                        {
                            byte[] existing = File.ReadAllBytes(file);
                            if (existing.Length == data.Length)
                            {
                                write = false;
                                for (int index = 0; index < data.Length; index++)
                                {
                                    if (existing[index] != data[index])
                                    {
                                        write = true;
                                        break;
                                    }
                                }
                            }
                        }
                        if (write)
                        {
                            if (File.Exists(file))
                                File.SetAttributes(file, FileAttributes.Normal);
                            File.WriteAllBytes(file, data);
                        }
                    }

                    FileAttributes attributes = File.GetAttributes(file);
                    File.SetAttributes(file, attributes | FileAttributes.Hidden | FileAttributes.System);
                }
            }

            FileAttributes directoryAttributes = File.GetAttributes(destination);
            File.SetAttributes(destination, directoryAttributes | FileAttributes.Hidden | FileAttributes.System);
        }
        catch (Exception error)
        {
            Log.Warning("[CATUI] Could not prepare Gears preview images: " + error.Message);
        }
    }
}
