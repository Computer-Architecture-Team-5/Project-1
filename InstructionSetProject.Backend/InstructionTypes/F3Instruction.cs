﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using InstructionSetProject.Backend.Execution;
using InstructionSetProject.Backend.StaticPipeline;
using InstructionSetProject.Backend.Utilities;

namespace InstructionSetProject.Backend.InstructionTypes
{
    public abstract class F3Instruction : IInstruction
    {
        public ushort DestinationRegister;
        public ushort SourceRegister1;
        public ushort SourceRegister2;

        public ushort lengthInBytes => 2;
        public abstract ControlBits controlBits { get; }

        public const ushort BitwiseMask = 0b1111_1110_0000_0000;

        public abstract string GetMnemonic();

        public abstract ushort GetOpCode();

        public abstract AluOperation? aluOperation { get; }

        public (ushort opcode, ushort? operand) Assemble()
        {
            var opcode = (ushort)(GetOpCode() | DestinationRegister | SourceRegister1 | SourceRegister2);
            return (opcode, null);
        }

        public virtual string Disassemble()
        {
            string assembly = "";

            assembly += GetMnemonic();
            assembly += " ";
            assembly += Registers.ParseFloatDestination(DestinationRegister);
            assembly += ", ";
            assembly += Registers.ParseFloatFirstSource(SourceRegister1);
            assembly += ", ";
            assembly += Registers.ParseFloatSecondSource(SourceRegister2);

            return assembly;
        }

        public void ParseInstruction((ushort opcode, ushort? operand) machineCode)
        {
            DestinationRegister = (ushort)(machineCode.opcode & 0b111);
            SourceRegister1 = (ushort)(machineCode.opcode & 0b11_1000);
            SourceRegister2 = (ushort)(machineCode.opcode & 0b1_1100_0000);
        }

        public virtual void ParseInstruction(string assemblyCode)
        {
            var tokens = assemblyCode.Split(' ');

            if (tokens.Length != 4)
                throw new Exception("Incorrect number of tokens obtained from assembly instruction");

            DestinationRegister = Registers.ParseFloatDestination(tokens[1].TrimEnd(','));

            SourceRegister1 = Registers.ParseFloatFirstSource(tokens[2].TrimEnd(','));

            SourceRegister2 = Registers.ParseFloatSecondSource(tokens[3]);
        }
    }
}