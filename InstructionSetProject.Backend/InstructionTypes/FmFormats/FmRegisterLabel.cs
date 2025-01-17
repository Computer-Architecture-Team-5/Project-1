﻿using InstructionSetProject.Backend.Utilities;

namespace InstructionSetProject.Backend.InstructionTypes.FmFormats
{
    public abstract class FmRegisterLabel : FmInstruction, IImmediateInstruction, ILabelInstruction
    {
        public override ushort? addressingMode { get => null; set { } }
        public override ushort? secondRegister { get => null; set { } }
        public override RegisterType? secondRegisterType => null;
        public override RegisterType? firstRegisterType => RegisterType.Read;

        public override string Disassemble()
        {
            string assembly = "";

            assembly += GetMnemonic();
            assembly += " ";
            assembly += Registers.ParseFirstFloat(firstRegister ?? 0);
            assembly += ", ";
            assembly += (immediate ?? 0).ToString("X2");

            return assembly;
        }

        public override void ParseInstruction(string assemblyCode)
        {
            var tokens = assemblyCode.Split(' ');

            if (tokens.Length != 3)
                throw new Exception("Incorrect number of tokens obtained from assembly instruction");

            firstRegister = Registers.ParseFirstFloat(tokens[1].TrimEnd(','));

            immediate = Convert.ToUInt16(tokens[2], 16);
        }

        public ushort GenerateImmediate()
        {
            return immediate ?? 0;
        }

        public bool CheckForLabel(string line)
        {
            var tokens = line.Split(' ');
            if (tokens.Length != 3)
                return false;
            var possibleLabel = tokens[2];
            return !UInt16.TryParse(possibleLabel, out var result);
        }
    }
}
