using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ModLoader.Helpers;
using SFS.UI;
using SFS.UI.ModGUI;
using SFS.World;
using UnityEngine;
using VanillaUpgrades.Utility;
using VanillaUpgrades.World.Patches;
using UIExtensions = VanillaUpgrades.Utility.UIExtensions;

namespace VanillaUpgrades.World
{
    public static partial class AdvancedInfo
    {
        // Key fields in a single map
        private static readonly Dictionary<string, DisplayRefs> DisplayMap = new()
        {
            { "Apoapsis", new DisplayRefs() },
            { "Periapsis", new DisplayRefs() },
            { "Eccentricity", new DisplayRefs() },
            { "Angle", new DisplayRefs() },
            { "Local Angle", new DisplayRefs() }
        };

        // Inline UI objects to hide/show
        private static readonly List<GameObject> VanillaInfoObjects = new();

        private static GameObject windowHolder;
        private static Window advancedInfoWindow;
        private static Container verticalContainer;
        private static Container horizontalContainer;

        // --------------------------------------------------------------------
        // SETUP / INITIALIZATION
        // --------------------------------------------------------------------
        public static void Setup()
        {
            // Create a parent GameObject for the separate window UI
            windowHolder = UIExtensions.ZeroedHolder(Builder.SceneToAttach.CurrentScene, "AdvancedInfoHolder");

            // Build references for the vanilla inline UI
            SetupVanillaUI();

            // Build references for the separate window (both vertical/horizontal)
            CreateWindowUI();

            // Event listeners
            PlayerController.main.player.OnChange += OnPlayerChange;
            Config.settings.horizontalMode.OnChange += CheckHorizontalToggle;
            Config.settings.persistentVars.windowScale.OnChange += () => advancedInfoWindow?.ScaleWindow();
            Config.settings.showAdvanced.OnChange += OnToggle;
            Config.settings.showAdvancedInSeparateWindow.OnChange += OnToggle;

            // Initial toggle
            OnToggle();

            // Periodic update
            UpdateInGame.execute += Update;
            SceneHelper.OnWorldSceneUnloaded += OnDestroy;
        }

        private static void OnDestroy()
        {
            VanillaInfoObjects.Clear();

            // Unsubscribe from events
            PlayerController.main.player.OnChange -= OnPlayerChange;
            Config.settings.horizontalMode.OnChange -= CheckHorizontalToggle;
            Config.settings.persistentVars.windowScale.OnChange -= () => advancedInfoWindow?.ScaleWindow();
            Config.settings.showAdvanced.OnChange -= OnToggle;
            Config.settings.showAdvancedInSeparateWindow.OnChange -= OnToggle;

            // Clear references
            foreach (var key in DisplayMap.Keys.ToList())
                DisplayMap[key] = new DisplayRefs();
        }

        // --------------------------------------------------------------------
        // MAIN UPDATE
        // --------------------------------------------------------------------
        private static void Update()
        {
            if (WorldManager.currentRocket == null) return;
            RefreshAllFields();
        }

        // --------------------------------------------------------------------
        // TOGGLES & EVENT HANDLERS
        // --------------------------------------------------------------------
        private static void OnToggle()
        {
            if (PlayerController.main == null) return;

            var showAdvanced = WorldManager.currentRocket != null && Config.settings.showAdvanced;
            bool separate = Config.settings.showAdvancedInSeparateWindow;

            // Toggle the separate window GameObject
            windowHolder.SetActive(showAdvanced && separate);

            // Toggle the vanilla inline objects
            VanillaInfoObjects.ForEach(go => go.SetActive(showAdvanced && !separate));
        }

        private static void CheckHorizontalToggle()
        {
            if (advancedInfoWindow == null) return;

            bool isHorizontal = Config.settings.horizontalMode;

            verticalContainer?.gameObject.SetActive(!isHorizontal);
            horizontalContainer?.gameObject.SetActive(isHorizontal);

            // Adjust the window size for each layout
            advancedInfoWindow.Size = isHorizontal ? new Vector2(350, 260) : new Vector2(220, 390);

            advancedInfoWindow.ClampWindow();
        }

        private static void OnPlayerChange()
        {
            if (PlayerController.main == null) return;
            OnToggle();
            ToggleTorque.Set(false);
        }

        // --------------------------------------------------------------------
        // REFRESHING
        // --------------------------------------------------------------------
        private static void RefreshAllFields()
        {
            GetOrbitValues(
                out var apo,
                out var peri,
                out var ecc,
                out var angle,
                out var localAngle
            );

            SetField("Apoapsis", apo);
            SetField("Periapsis", peri);
            SetField("Eccentricity", ecc);
            SetField("Local Angle", localAngle);
            SetField("Angle", angle);
        }

        private static void SetField(string fieldKey, string value)
        {
            if (!DisplayMap.TryGetValue(fieldKey, out DisplayRefs refs) || refs == null)
                return;

            bool separate = Config.settings.showAdvancedInSeparateWindow;
            bool isHorizontal = Config.settings.horizontalMode;

            if (separate)
            {
                switch (isHorizontal)
                {
                    // Show in separate window
                    case true when refs.horizontal != null:
                        refs.horizontal.Text = value;
                        break;
                    case false when refs.vertical != null:
                        refs.vertical.Text = value;
                        break;
                }
            }
            else
            {
                // Show in vanilla
                if (refs.vanilla != null) refs.vanilla.Text = value;
            }
        }

        private static void GetOrbitValues(
            out string apoapsis,
            out string periapsis,
            out string eccentricity,
            out string angle,
            out string localAngle
        )
        {
            apoapsis = "0.0m";
            periapsis = "0.0m";
            eccentricity = "0.000";
            localAngle = "0.0°";
            angle = "0.0°";

            Rocket rocket = WorldManager.currentRocket;
            if (rocket == null) return;

            if (rocket.physics.GetTrajectory().paths[0] is Orbit orbit)
            {
                var planetRadius = rocket.location.planet.Value.Radius;
                apoapsis = (orbit.apoapsis - planetRadius).ToDistanceString();

                var realPeri = orbit.periapsis < planetRadius ? 0 : orbit.periapsis - planetRadius;
                periapsis = realPeri.ToDistanceString();

                eccentricity = orbit.ecc.ToString("F3", CultureInfo.InvariantCulture);
            }

            var globalAngle = rocket.partHolder.transform.eulerAngles.z;
            Location loc = rocket.location.Value;

            Vector2 orbitAngleVector = new Vector2(
                Mathf.Cos((float)loc.position.AngleRadians),
                Mathf.Sin((float)loc.position.AngleRadians)
            ).Rotate_Radians(270 * Mathf.Deg2Rad);

            var facing = new Vector2(Mathf.Cos(globalAngle * Mathf.Deg2Rad), Mathf.Sin(globalAngle * Mathf.Deg2Rad));
            var trueAngle = Vector2.SignedAngle(facing, orbitAngleVector);

            localAngle = trueAngle.ToString("F1", CultureInfo.InvariantCulture) + "°";
            angle = globalAngle > 180
                ? (360 - globalAngle).ToString("F1", CultureInfo.InvariantCulture) + "°"
                : (-globalAngle).ToString("F1", CultureInfo.InvariantCulture) + "°";
        }

        // -- Holds references for each field's UI in vanilla inline, horizontal, and vertical
        private class DisplayRefs
        {
            public Label horizontal; // readout label in horizontal layout
            public TextAdapter vanilla; // e.g. right label in vanilla UI
            public Label vertical; // readout label in vertical layout
        }
    }
}