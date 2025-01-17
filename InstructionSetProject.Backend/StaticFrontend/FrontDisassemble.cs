﻿namespace InstructionSetProject.Backend.StaticFrontend
{
    public static class FrontDisassemble
    {
        public static string output { get; set; }

        public static List<byte> machineCode;
        public static string machineCodeString { get; set; }

        public static int totalInstructions { get; set; }
        public static int arithmeticInstructions { get; set; }
        public static int generalInstructions { get; set; }
        public static int jumpInstructions { get; set; }
        public static int stackInstructions { get; set; }

        public static int immediateAddrMode { get; set; }
        public static int directAddrMode { get; set; }
        public static int indirectAddrMode { get; set; }
        public static int registerDirectAddrMode { get; set; }
        public static int registerIndirectAddrMode { get; set; }
        public static int indexDirectAddrMode { get; set; }
        public static int indexIndirectAddrMode { get; set; }
        public static int indexOffsetAddrMode { get; set; }
        public static int indexDefferedAddrMode { get; set; }
        public static int stackDirectAddrMode { get; set; }
        public static int stackIndirectAddrMode { get; set; }
        public static int stackOffsetAddrMode { get; set; }
        public static int stackIndexDirectAddrMode { get; set; }
        public static int stackIndexIndirectAddrMode { get; set; }
        public static int stackIndexOffsetAddrMode { get; set; }
        public static int stackIndexDefferedAddrMode { get; set; }

        public static string assemblyCode = "";

        public static string Disassemble(string currentCodeDis)
        {

            machineCodeString = currentCodeDis.Replace(" ", string.Empty);
            machineCode = HexStringToByteList(machineCodeString);
            output = Disassembler.Disassemble(machineCode);
            assemblyCode = Disassembler.assemblyCode;

            totalInstructions = Disassembler.totalInstructions;
            arithmeticInstructions = Disassembler.arithmeticInstructions;
            generalInstructions = Disassembler.generalInstructions;
            jumpInstructions = Disassembler.jumpInstructions;
            stackInstructions = Disassembler.stackInstructions;

            immediateAddrMode = Disassembler.immediateAddrMode;
            directAddrMode = Disassembler.directAddrMode;
            indirectAddrMode = Disassembler.indirectAddrMode;
            registerDirectAddrMode = Disassembler.registerDirectAddrMode;
            registerIndirectAddrMode = Disassembler.registerIndirectAddrMode;
            indexDirectAddrMode = Disassembler.indexDirectAddrMode;
            indexIndirectAddrMode = Disassembler.indexIndirectAddrMode;
            indexOffsetAddrMode = Disassembler.indexOffsetAddrMode;
            indexDefferedAddrMode = Disassembler.indexDefferedAddrMode;
            stackDirectAddrMode = Disassembler.stackDirectAddrMode;
            stackIndirectAddrMode = Disassembler.stackIndirectAddrMode;
            stackOffsetAddrMode = Disassembler.stackOffsetAddrMode;
            stackIndexDirectAddrMode = Disassembler.stackIndexDirectAddrMode;
            stackIndexIndirectAddrMode = Disassembler.stackIndexIndirectAddrMode;
            stackIndexOffsetAddrMode = Disassembler.stackIndexOffsetAddrMode;
            stackIndexDefferedAddrMode = Disassembler.stackIndexDefferedAddrMode;

            return OutputFromDis(output);
        }

        private static List<byte> HexStringToByteList(string machineCodeString)
        {
            if (machineCodeString.Length % 2 == 1)
                throw new Exception("Cannot have an odd number of digits!!");

            int numChars = machineCodeString.Length;
            byte[] bytes = new byte[numChars / 2];
            for (int i = 0; i < numChars; i += 2)
                bytes[i / 2] = Convert.ToByte(machineCodeString.Substring(i, 2), 16);

            List<byte> mCode = new List<byte>(bytes);

            return mCode;
        }

        public static string OutputFromDis(string assembly)
        {
            string returnStr = "";

            returnStr += assembly;

            return returnStr;
        }
    }
}
