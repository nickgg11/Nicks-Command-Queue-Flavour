using System;
using System.Collections;
using System.Security.Permissions;
using BepInEx;
using CommandQueue.Features;
using CommandQueue.UI;
using RoR2;
using RoR2.UI;
using UnityEngine;
using UnityEngine.Networking;

/*
TODO:
Current tasks:
- Provide a way to setup (per-character) initial queues
 - Adapt ItemSetConfigWrapper from AutoItemPickup
 - Generate a short description in comments
 - Generate sections named after characters
 - Parse section contents for items
  - Decide on format
  ? \s* ItemName \s* \* Count \s* ,

Future tasks:
- Maybe create UI that shows other players' queue
 - Should players broadcast their queues even if server isn't aware?
*/

#pragma warning disable CS0618 // Type or member is obsolete
[assembly: SecurityPermission(SecurityAction.RequestMinimum, SkipVerification = true)]
#pragma warning restore CS0618 // Type or member is obsolete

namespace CommandQueue
{
    [BepInPlugin("com.kuberoot.commandqueue", "CommandQueue", "2.1.0")]
    public class CommandQueue : BaseUnityPlugin
    {
        public static event Action PluginUnloaded;
        public static bool IsLoaded;

        private static GameObject commandUIPrefab;

        // The command cube prefab is loaded asynchronously, so resolve the panel lazily instead of
        // trying to catch the moment it becomes available.
        internal static GameObject CommandUIPrefab
        {
            get
            {
                if (!commandUIPrefab)
                {
                    var cube = RoR2.Artifacts.CommandArtifactManager.commandCubePrefab;
                    commandUIPrefab = cube ? cube.GetComponent<PickupPickerController>()?.panelPrefab : null;
                }
                return commandUIPrefab;
            }
        }

        public void Awake()
        {
            ModConfig.OnReady += () =>
            {
                ModConfig.enabledTabs.SettingChanged += (_, __) => PartialReload();
                ModConfig.bigItemButtonContainer.SettingChanged += (_, __) => PartialReload();
                ModConfig.bigItemButtonScale.SettingChanged += (_, __) => PartialReload();
                ModConfig.showQueuePreview.SettingChanged += (_, __) => UpdateQueuePreview();
                UpdateQueuePreview();
            };

            TierInfo.Init();
            ModConfig.InitConfig(Config);
        }

        // Optional feature, see Features/QueuePreviewHud.cs.
        private static void UpdateQueuePreview()
        {
            if (IsLoaded && ModConfig.Ready && ModConfig.showQueuePreview.Value)
                QueuePreviewHud.Enable();
            else
                QueuePreviewHud.Disable();
        }

        private void PartialReload()
        {
            QueueManager.UpdateQueueAvailability();

            // Include inactive ones: the scoreboard is hidden most of the time, and its UI must be torn
            // down too so it rebuilds with the new settings when shown.
            foreach (var uiManager in FindObjectsOfType<UIManager>(true))
                uiManager.RebuildUI();
        }

        public void OnEnable()
        {
            IsLoaded = true;

            On.RoR2.PickupPickerController.OnDisplayBegin += HandleCommandDisplayBegin;
            On.RoR2.GenericPickupController.PickupMessage.Deserialize += HandlePickupMessage;
            On.RoR2.UI.ScoreboardController.Awake += ScoreboardController_Awake;
            QueueManager.Enable();
            UpdateQueuePreview();

            foreach (var scoreboard in FindObjectsOfType<ScoreboardController>(true))
            {
                if (!scoreboard.GetComponent<UIManager>())
                    scoreboard.gameObject.AddComponent<UIManager>();
            }

            TryEnableROOIntegration();
        }

        public void OnDisable()
        {
            IsLoaded = false;
            PluginUnloaded?.Invoke();
            On.RoR2.PickupPickerController.OnDisplayBegin -= HandleCommandDisplayBegin;
            On.RoR2.GenericPickupController.PickupMessage.Deserialize -= HandlePickupMessage;
            On.RoR2.UI.ScoreboardController.Awake -= ScoreboardController_Awake;
            QueueManager.Disable();
            QueuePreviewHud.Disable();

            TryDisableROOIntegration();
        }

        public void Start()
        {
            TryEnableROOIntegration();
        }

        public RiskOfOptionsIntegration ROOIntegration;
        private static bool rooOptionsRegistered;

        private void TryEnableROOIntegration()
        {
            // Risk of Options can't remove options, so only register them once per session.
            if (ROOIntegration == null && !rooOptionsRegistered)
            {
                if(BepInEx.Bootstrap.Chainloader.PluginInfos.ContainsKey("com.rune580.riskofoptions"))
                {
                    ROOIntegration = new RiskOfOptionsIntegration();
                    ROOIntegration.Enable();
                    rooOptionsRegistered = true;
                }
            }
        }

        private void TryDisableROOIntegration()
        {
            ROOIntegration?.Disable();
            ROOIntegration = null;
        }

        private void HandleCommandDisplayBegin(On.RoR2.PickupPickerController.orig_OnDisplayBegin orig, PickupPickerController self, NetworkUIPromptController networkUIPromptController, LocalUser localUser, CameraRigController cameraRigController)
        {
            var prefab = CommandUIPrefab;
            if (prefab && self.panelPrefab == prefab)
            {
                foreach(var (tier, pickupIndex) in QueueManager.PeekAllUnreserved())
                {
                    if (self.IsChoiceAvailable(pickupIndex))
                    {
                        self.StartCoroutine(SubmitQueuedChoiceNextFrame(self, tier, pickupIndex, networkUIPromptController, localUser, cameraRigController));
                        return;
                    }
                }
            }

            orig(self, networkUIPromptController, localUser, cameraRigController);
        }

        private static IEnumerator SubmitQueuedChoiceNextFrame(PickupPickerController self, ItemTier tier, PickupIndex pickupIndex, NetworkUIPromptController networkUIPromptController, LocalUser localUser, CameraRigController cameraRigController)
        {
            yield return null;

            if (!self)
                yield break;

            int choiceIndex = FindAvailableOption(self, pickupIndex);
            if (choiceIndex < 0)
            {
                // The options changed under us, show the normal menu rather than leaving the player stuck.
                self.OnDisplayBegin(networkUIPromptController, localUser, cameraRigController);
                yield break;
            }

            // The item drops on the ground, it only leaves the queue once it's picked up (see HandlePickupMessage).
            // Only permanent items count towards the queue, so others don't reserve a slot either.
            if (IsPermanent(self.options[choiceIndex].pickup) && QueueManager.PeekUnreserved(tier) == pickupIndex)
                QueueManager.Reserve(tier, pickupIndex);

            SubmitChoiceDirect(self, choiceIndex);
        }

        private static int FindAvailableOption(PickupPickerController self, PickupIndex pickupIndex)
        {
            var options = self.options;
            for (int i = 0; i < options.Length; i++)
            {
                if (options[i].pickup.pickupIndex == pickupIndex && options[i].available)
                    return i;
            }
            return -1;
        }

        // Same as PickupPickerController.SubmitChoice, but without going through hooks on it.
        // LookingGlass hooks SubmitChoice to map indices of its re-sorted panel back to option
        // indices, and only rebuilds that map when a panel is displayed. We never display one, so
        // the map is left over from the last sorted picker and would turn our index into a different
        // item, or an out of range index that drops the selection entirely.
        private static void SubmitChoiceDirect(PickupPickerController self, int choiceIndex)
        {
            if (NetworkServer.active)
            {
                self.HandlePickupSelected(choiceIndex);
                return;
            }

            var promptController = self.networkUIPromptController;
            NetworkWriter writer = promptController.BeginMessageToServer();
            writer.Write(PickupPickerController.msgSubmit);
            writer.Write(choiceIndex);
            promptController.FinishMessageToServer(writer);
        }

        // Temporary items decay out of the inventory and ethereal (rental) items get taken back, so
        // neither counts towards the queue.
        private static bool IsPermanent(UniquePickup pickup)
        {
            return !pickup.isTempItem && !pickup.isRental;
        }

        // The server sends this to every client (host included) when someone picks up a pickup. Unlike
        // Inventory.RpcItemAdded it carries the full pickup state, so temporary and ethereal items can be
        // told apart, and it isn't sent for items that skip the ground (scrappers, item transforms and so on).
        private void HandlePickupMessage(On.RoR2.GenericPickupController.PickupMessage.orig_Deserialize orig, GenericPickupController.PickupMessage self, NetworkReader reader)
        {
            orig(self, reader);

            var localMaster = LocalUserManager.GetFirstLocalUser()?.cachedMaster;
            if (!localMaster || self.masterGameObject != localMaster.gameObject)
                return;

            if (IsPermanent(self.pickupState))
                QueueManager.OnItemPickedUp(self.pickupState.pickupIndex);
        }

        private void ScoreboardController_Awake(On.RoR2.UI.ScoreboardController.orig_Awake orig, ScoreboardController self)
        {
            self.gameObject.AddComponent<UIManager>();
            orig(self);
        }
    }
}
