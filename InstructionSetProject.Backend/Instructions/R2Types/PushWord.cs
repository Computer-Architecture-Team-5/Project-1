﻿using InstructionSetProject.Backend.Execution;
using InstructionSetProject.Backend.InstructionTypes.R2Formats;
using InstructionSetProject.Backend.StaticPipeline;
using InstructionSetProject.Backend.Utilities;

namespace InstructionSetProject.Backend.Instructions.R2Types
{
    public class PushWord : R2Register
    {
        public const string Mnemonic = "PSW";

        public const ushort OpCode = 0b0000_0001_0000_0000;

        public override RegisterType? firstRegisterType => RegisterType.Read;

        public override ControlBits controlBits => new(false, false, false, true, false, false, false);

        public override AluOperation? aluOperation => AluOperation.PassSecondOperandThrough;

        public override int cyclesNeededInMemory => 3;

        public override InstructionUnit instructionUnit => InstructionUnit.Memory;

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
