﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using InstructionSetProject.Backend.Utilities;

namespace InstructionSetProject.Backend.InstructionTypes.RmFormats
{
    public abstract class RmImmediate : RmInstruction, IImmediateInstruction
    {
        public override ushort? addressingMode { get => null; set { } }
        public override ushort? secondRegister { get => null; set { } }
        public override ushort? firstRegister { get => null; set { } }
        public override RegisterType? firstRegisterType => null;
        public override RegisterType? secondRegisterType => null;

        public override string Disassemble()
        {
            string assembly = "";

            assembly += GetMnemonic();
            assembly += " ";
            assembly += (immediate ?? 0).ToString("X2");

            return assembly;
        }

        public override void ParseInstruction(string assemblyCode)
        {
            var tokens = assemblyCode.Split(' ');

            if (tokens.Length != 2)
                throw new Exception("Incorrect number of tokens obtained from assembly instruction");

            immediate = Convert.ToUInt16(tokens[1], 16);
        }

        public ushort GenerateImmediate()
        {
            return immediate ?? 0;
        }
    }
}
