﻿using InstructionSetProject.Backend.Execution;
using InstructionSetProject.Backend.InstructionTypes.RmFormats;
using InstructionSetProject.Backend.StaticPipeline;

namespace InstructionSetProject.Backend.Instructions.RmTypes
{
    public class JumpBelowEqual : RmRegisterRegisterLabel
    {
        public const string Mnemonic = "JBE";

        public const ushort OpCode = 0b1100_1010_0000_0000;

        public override ControlBits controlBits => throw new NotImplementedException();

        public override AluOperation? aluOperation => null;

        public override string GetMnemonic()
        {
            return Mnemonic;
        }

        public override ushort GetOpCode()
        {
            return OpCode;
        }
    }
}
