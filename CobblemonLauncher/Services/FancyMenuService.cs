using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace CobblemonLauncher.Services
{
    public class FancyMenuService
    {
        public static void ConfigureCustomMenu(string gameDir, string serverAddress)
        {
            string configDir = Path.Combine(gameDir, "config", "fancymenu", "customization");
            string titlePath = Path.Combine(configDir, "cm_title.txt");
            string cleanTitlePath = Path.Combine(configDir, "cm_title_clean.txt");

            string sourceFile = File.Exists(cleanTitlePath) ? cleanTitlePath : titlePath;

            if (!File.Exists(sourceFile))
            {
                return;
            }

            try
            {
                var lines = File.ReadAllLines(sourceFile, Encoding.UTF8);
                var outLines = new List<string>();

                var dropIds = new HashSet<string>
                {
                    "79a9bf34-dd4c-4e26-9e1b-66b7b1f577611680905829959", // Singleplayer
                    "f5bec8b8-b9bd-4530-84d0-31f20f1a27041680906054558", // Exit
                    "3b098015-a3c0-40cd-873f-d3b98110ae741680906452058", // Discord
                    "cfb7e55f-f4b1-4e98-b850-dbf927d2a478-1706869081785", // Language
                    "1f9c8f5f-1298-4211-9f07-f3c8bb190ff4-1706879179848", // Ticker
                    "a611062b-c03a-47a1-b465-ea2cc68712e1-1707040465598"  // Mods
                };

                string multiplayerId = "03ecb238-0067-40e6-8987-ffef076582b51680905964712";
                string optionsId = "8240814f-2642-49ff-8389-a766088a0a961680906226701";

                var currentBlock = new List<string>();
                bool inBlock = false;

                foreach (var line in lines)
                {
                    if (line.TrimStart().StartsWith("element {") || line.TrimStart().StartsWith("vanilla_button {"))
                    {
                        inBlock = true;
                        currentBlock.Clear();
                        currentBlock.Add(line);
                    }
                    else if (inBlock)
                    {
                        currentBlock.Add(line);
                        if (line.Trim() == "}")
                        {
                            inBlock = false;
                            string blockText = string.Join("\n", currentBlock);

                            bool shouldDrop = false;
                            foreach (var dropId in dropIds)
                            {
                                if (blockText.Contains(dropId))
                                {
                                    shouldDrop = true;
                                    break;
                                }
                            }

                            if (!shouldDrop)
                            {
                                for (int i = 0; i < currentBlock.Count; i++)
                                {
                                    string l = currentBlock[i];
                                    if (blockText.Contains(multiplayerId))
                                    {
                                        if (l.TrimStart().StartsWith("label ="))
                                        {
                                            currentBlock[i] = "  label = Start game";
                                        }
                                        if (l.Contains("[action_type:mimicbutton]") && !string.IsNullOrWhiteSpace(serverAddress))
                                        {
                                            currentBlock[i] = $"  [executable_action_instance:49a0631b-7117-4625-ad12-ec1beceaa9ea-1706832365143][action_type:joinserver] = {serverAddress.Trim()}";
                                        }
                                        if (l.TrimStart().StartsWith("y ="))
                                        {
                                            currentBlock[i] = "  y = 30";
                                        }
                                    }
                                    else if (blockText.Contains(optionsId))
                                    {
                                        if (l.TrimStart().StartsWith("label ="))
                                        {
                                            currentBlock[i] = "  label = Cài đặt";
                                        }
                                        if (l.TrimStart().StartsWith("y ="))
                                        {
                                            currentBlock[i] = "  y = 70";
                                        }
                                    }
                                    outLines.Add(currentBlock[i]);
                                }
                            }
                        }
                    }
                    else
                    {
                        outLines.Add(line);
                    }
                }

                File.WriteAllLines(titlePath, outLines, Encoding.UTF8);

                // Also mirror to layouts folder
                string layoutsDir = Path.Combine(gameDir, "config", "fancymenu", "layouts");
                Directory.CreateDirectory(layoutsDir);
                File.WriteAllLines(Path.Combine(layoutsDir, "title_screen.txt"), outLines, Encoding.UTF8);
                File.WriteAllLines(Path.Combine(layoutsDir, "cm_title.txt"), outLines, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating FancyMenu layout: {ex.Message}");
            }
        }
    }
}
