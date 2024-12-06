using Modding;
using System;
using System.Reflection;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using UnityEngine;
using Satchel.BetterMenus;

namespace BetterModList {
    public class BetterModList: Mod, ICustomMenuMod, IGlobalSettings<GlobalSettings> {
        new public string GetName() => "BetterModList";
        public override string GetVersion() => "1.0.0.0";

        private Menu MenuRef;
        public static GlobalSettings gs = new();
        private GUIStyle styleRef;

        private static MethodInfo mvdOnGUI = typeof(ModVersionDraw).GetMethod("OnGUI", BindingFlags.Public | BindingFlags.Instance);
        private ILHook ilMvdOnGUI;

        private string firstHalfStorage = "";
        private string secondHalfStorage = "";
        private string fullList = "";
        private bool isUpdatingGUI = false;

        public override void Initialize() {
            styleRef = typeof(ModVersionDraw).GetField("style", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null) as GUIStyle;
            ilMvdOnGUI = new ILHook(mvdOnGUI, OnGUI);
        }

        private void OnGUI(ILContext il) {
            ILCursor cursor = new ILCursor(il).Goto(0);
            cursor.GotoNext(i => i.MatchCall<GUI>("Label"));
            cursor.GotoNext(i => i.MatchRet());
            cursor.Emit(OpCodes.Ldarg_0);
            cursor.EmitDelegate<Action<ModVersionDraw>>(AddNewUI);
        }

        private void AddNewUI(ModVersionDraw self) {
            bool skip = true;
            if(isUpdatingGUI) {
                isUpdatingGUI = false;
                skip = false;
            }
            else if(self.drawString != firstHalfStorage) {
                fullList = self.drawString;
                skip = false;
            }
            if(!skip) {
                (string, string) halves = parseDrawString(fullList);
                self.drawString = halves.Item1;
                firstHalfStorage = halves.Item1;
                secondHalfStorage = halves.Item2;
            }
            styleRef.fontSize = gs.fontSize;
            GUI.Label(new Rect(0f, 0f, Screen.width, Screen.height), secondHalfStorage, new GUIStyle(styleRef) { alignment = UnityEngine.TextAnchor.UpperRight });
        }

        private (string, string) parseDrawString(string drawString) {
            string[] lines = drawString.Split('\n');
            int lineCount = lines.Length;
            bool doLoop = true;
            while(doLoop) {
                if(string.IsNullOrWhiteSpace(lines[lineCount - 1])) {
                    lineCount--;
                }
                else {
                    doLoop = false;
                }
            }
            if(!gs.distributeEnabled) {
                if(gs.leftSide) {
                    return (drawString, "");
                }
                else {
                    string rightSide = "";
                    for(int i = 1; i < lineCount; i++) {
                        rightSide += lines[i] + "\n";
                    }
                    return (lines[0], rightSide);
                }
            }
            int division = gs.evenSplit ? (int)Math.Ceiling((double)lineCount / 2) + (gs.leftSide ? 0 : 1) : Math.Min((int)gs.maxMods,lineCount);
            string firstHalf = "";
            string secondHalf = "";
            for(int i = 1; i < division; i++) {
                firstHalf += lines[i] + "\n";
            }
            for(int i = division; i < lineCount; i++) {
                secondHalf += lines[i] + "\n";
            }
            if(gs.leftSide)
                return ($"{lines[0]}\n{firstHalf}", secondHalf);
            return ($"{lines[0]}\n{secondHalf}", firstHalf);
        }

        public MenuScreen GetMenuScreen(MenuScreen modListMenu, ModToggleDelegates? modtoggledelegates) {
            MenuRef ??= new Menu(
                name: "Better Mod List",
                elements: new Element[] {
                    new HorizontalOption(
                        name: "Side",
                        description: "",
                        values: new string[] { "Left\n", "Right\n" },
                        applySetting: index => {
                            gs.leftSide = index == 0;
                            isUpdatingGUI = true;
                        },
                        loadSetting: () => gs.leftSide ? 0 : 1
                    ),
                    new HorizontalOption(
                        name: "Distribution",
                        description: "",
                        values: new string[] { "Fill to max\n", "Even split\n", "Disabled\n" },
                        applySetting: index => {
                            gs.distributeEnabled = index != 2;
                            gs.evenSplit = index == 1;
                            isUpdatingGUI = true;
                        },
                        loadSetting: () => gs.distributeEnabled ? gs.evenSplit ? 1 : 0 : 2),
                    new CustomSlider(
                        name: "Max length",
                        storeValue: val => {
                            gs.maxMods = (int)val;
                            isUpdatingGUI = true;
                        },
                        loadValue: () => (int)gs.maxMods,
                        minValue: 1,
                        maxValue: 100,
                        wholeNumbers: true
                    ),
                    new CustomSlider(
                        name: "Font size",
                        storeValue: val => {
                            gs.fontSize = (int)val;
                        },
                        loadValue: () => (int)gs.fontSize,
                        minValue: 8,
                        maxValue: 32,
                        wholeNumbers: true
                    )
                }
            );
            return MenuRef.GetMenuScreen(modListMenu);
        }

        public bool ToggleButtonInsideMenu {
            get;
        }

        public void OnLoadGlobal(GlobalSettings s) {
            gs = s;
        }

        public GlobalSettings OnSaveGlobal() {
            return gs;
        }
    }

    public class GlobalSettings {
        public bool distributeEnabled = true;
        public float maxMods = 50;
        public bool evenSplit = false;
        public bool leftSide = true;
        public int fontSize = 13;
    }
}