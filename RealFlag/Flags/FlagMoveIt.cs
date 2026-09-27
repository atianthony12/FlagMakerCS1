using System;
using System.Collections.Generic;
using MoveItIntegration;

namespace RealFlag.Flags
{
    /// <summary>
    /// Found by Move It (its IntegrationHelper looks through every enabled mod's exported types
    /// for an IMoveItIntegrationFactory): how Move It carries a flag's design along.
    /// </summary>
    public sealed class FlagMoveItFactory : IMoveItIntegrationFactory
    {
        public MoveItIntegrationBase GetInstance()
        {
            return new FlagMoveIt();
        }
    }

    /// <summary>
    /// Move It's copy, paste, undo and export for flags. Read from Move It 2.10.9's IL
    /// (2026-09-25): MoveableProp.SaveToState calls every integration's Copy for a PROP, as it
    /// does for nodes, segments, buildings and trees; CloneActionBase.Do calls Paste on each
    /// clone with what Copy returned (Ctrl+C / Ctrl+V, duplicate, mirror); and
    /// BulldozeAction.UndoImplementation calls Paste on the prop it brings back. Without this, a
    /// pasted flag would get the new flag's design instead of the original's.
    /// </summary>
    /// <remarks>
    /// The record is the FlagSpec itself; Move It's exports (its XML) carry it as base 64 of the
    /// same bytes the city saves (FlagCodec). Paste may come on another thread than the main
    /// one: FlagRegistry is locked.
    /// </remarks>
    public sealed class FlagMoveIt : MoveItIntegrationBase
    {
        public override string ID
        {
            get { return "RealFlag"; }
        }

        public override string Name
        {
            get { return Mod.DisplayName; }
        }

        public override string Description
        {
            get { return "Flag designs"; }
        }

        public override Version DataVersion
        {
            get { return new Version(1, 0); }
        }

        public override object Copy(InstanceID sourceInstanceID)
        {
            if (sourceInstanceID.Type != InstanceType.Prop)
                return null;

            FlagSpec spec;
            return FlagRegistry.TryGet(sourceInstanceID.Prop, out spec) ? (object)spec : null;
        }

        public override void Paste(InstanceID targetInstanceID, object record, Dictionary<InstanceID, InstanceID> sourceMap)
        {
            if (targetInstanceID.Type != InstanceType.Prop || !(record is FlagSpec))
                return;

            FlagRegistry.Set(targetInstanceID.Prop, (FlagSpec)record);
            if (Log.Detailed)
                Log.Detail("flags: Move It pasted a flag's design onto prop #" + targetInstanceID.Prop);
        }

        public override string Encode64(object record)
        {
            if (!(record is FlagSpec))
                return null;

            return Convert.ToBase64String(FlagCodec.EncodeOne((FlagSpec)record));
        }

        public override object Decode64(string base64Data, Version dataVersion)
        {
            if (string.IsNullOrEmpty(base64Data))
                return null;

            try
            {
                FlagSpec spec;
                return FlagCodec.TryDecodeOne(Convert.FromBase64String(base64Data), out spec) ? (object)spec : null;
            }
            catch (FormatException)
            {
                return null;
            }
        }
    }
}
