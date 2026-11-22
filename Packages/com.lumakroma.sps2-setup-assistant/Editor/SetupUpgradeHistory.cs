using UnityEditor;
using UnityEngine;

namespace LumaKroma.Sps2SetupAssistant.Editor
{
    [FilePath("UserSettings/LumaKroma.SpsSetup.UpgradeNews.asset", FilePathAttribute.Location.ProjectFolder)]
    internal sealed class SetupUpgradeHistory : ScriptableSingleton<SetupUpgradeHistory>
    {
        [SerializeField] internal SetupUpgradeReceipt receipt = new SetupUpgradeReceipt();
        internal void Persist() => Save(true);
    }
}
