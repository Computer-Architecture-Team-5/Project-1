﻿using InstructionSetProject.Backend.Execution;
using InstructionSetProject.Backend.InstructionTypes.F2Formats;
using InstructionSetProject.Backend.StaticPipeline;

namespace InstructionSetProject.Backend.Instructions.F2Types
{
    public class Ceiling : F2RegisterRegister
    {
        public const string Mnemonic = "CEL";

        public const ushort OpCode = 0b0010_0000_0100_0000;

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
