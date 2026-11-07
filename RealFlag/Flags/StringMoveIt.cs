using System;
using System.Collections.Generic;
using MoveItIntegration;

namespace RealFlag.Flags
{
    /// <summary>
    /// Move It's integration for strings: a span's design goes with Move It's copy, paste, clone
    /// and export files. Move It calls Copy for every instance it keeps (props, nodes, segments:
    /// InstanceState.SaveIntegrations, read from its IL), and Paste for each new one; a node has
    /// no design of its own. Its own ID beside the flags' ("RealFlag"), so each reads only its own.
    /// </summary>
    public sealed class StringMoveItFactory : IMoveItIntegrationFactory
    {
        public MoveItIntegrationBase GetInstance()
        {
            return new StringMoveIt();
        }
    }

    public sealed class StringMoveIt : MoveItIntegrationBase
    {
        public override string ID
        {
            get { return "RealFlag.Strings"; }
        }

        public override string Name
        {
            get { return Mod.DisplayName; }
        }

        public override string Description
        {
            get { return "String flag designs"; }
        }

        public override Version DataVersion
        {
            get { return new Version(1, 0); }
        }

        public override object Copy(InstanceID sourceInstanceID)
        {
            if (sourceInstanceID.Type != InstanceType.NetSegment)
                return null;

            StringSpec spec;
            return StringRegistry.TryGet(sourceInstanceID.NetSegment, out spec) ? (object)spec : null;
        }

        public override void Paste(InstanceID targetInstanceID, object record, Dictionary<InstanceID, InstanceID> sourceMap)
        {
            if (targetInstanceID.Type != InstanceType.NetSegment || !(record is StringSpec))
                return;

            StringRegistry.Set(targetInstanceID.NetSegment, (StringSpec)record);
            if (Log.Detailed)
                Log.Detail("strings: Move It pasted a span's design onto segment #" + targetInstanceID.NetSegment);
        }

        public override string Encode64(object record)
        {
            if (!(record is StringSpec))
                return null;

            return Convert.ToBase64String(StringCodec.EncodeOne((StringSpec)record));
        }

        public override object Decode64(string base64Data, Version dataVersion)
        {
            if (string.IsNullOrEmpty(base64Data))
                return null;

            try
            {
                StringSpec spec;
                return StringCodec.TryDecodeOne(Convert.FromBase64String(base64Data), out spec) ? (object)spec : null;
            }
            catch (FormatException)
            {
                return null;
            }
        }
    }
}
