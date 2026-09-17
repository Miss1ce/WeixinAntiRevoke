using System;
using System.Collections.Generic;
using Iced.Intel;

namespace WeixinAntiRevoke
{
    internal sealed class PeImage
    {
        internal sealed class Section
        {
            public int Raw, Size;
            public uint Rva, Flags;
        }
        internal readonly byte[] Data;
        internal readonly List<Section> Sections = new List<Section>();
        private readonly int functions, functionCount;

        internal PeImage(byte[] data)
        {
            Data = data;
            Need(data.Length >= 256 && U16(0) == 0x5A4D, "不是有效的 PE 文件");
            int pe = checked((int)U32(0x3C));
            Need(U32(pe) == 0x4550 && U16(pe + 4) == 0x8664, "自动分析仅支持 x64 PE 文件");
            int count = U16(pe + 6), optSize = U16(pe + 20), opt = pe + 24;
            Need(count > 0 && count <= 96 && optSize >= 144 && U16(opt) == 0x20B, "PE 头结构不支持");
            Need(U32(opt + 108) >= 4, "缺少异常函数目录");
            for (int n = 0; n < count; n++)
            {
                int s = checked(opt + optSize + n * 40);
                Section section = new Section { Rva = U32(s + 12), Size = checked((int)U32(s + 16)), Raw = checked((int)U32(s + 20)), Flags = U32(s + 36) };
                Need(section.Raw >= 0 && section.Size >= 0 && (long)section.Raw + section.Size <= data.Length, "节范围无效");
                Sections.Add(section);
            }
            uint tableRva = U32(opt + 112 + 24), tableSize = U32(opt + 112 + 28);
            Need(tableSize > 0 && tableSize % 12 == 0 && tableSize <= 24 * 1024 * 1024, "函数表无效");
            functions = Offset(tableRva, checked((int)tableSize), false);
            functionCount = checked((int)tableSize / 12);
        }

        internal int Offset(ulong rva, int length, bool executable)
        {
            foreach (Section s in Sections)
                if (rva >= s.Rva && rva + (ulong)length >= rva && rva + (ulong)length <= (ulong)s.Rva + (ulong)s.Size && (!executable || (s.Flags & 0x20000000) != 0))
                    return checked(s.Raw + (int)(rva - s.Rva));
            throw new InvalidOperationException("地址不在有效的" + (executable ? "可执行" : "文件") + "节内");
        }

        internal uint Rva(int offset)
        {
            foreach (Section s in Sections)
                if (offset >= s.Raw && (long)offset < (long)s.Raw + s.Size)
                    return checked(s.Rva + (uint)(offset - s.Raw));
            throw new InvalidOperationException("文件偏移不在节内");
        }

        internal List<Instruction> FunctionAt(ulong rva)
        {
            int lo = 0, hi = functionCount - 1;
            while (lo <= hi)
            {
                int mid = lo + (hi - lo) / 2, p = functions + mid * 12;
                uint start = U32(p), end = U32(p + 4);
                if (rva < start) hi = mid - 1;
                else if (rva >= end) lo = mid + 1;
                else
                {
                    Need(end > start && end - start < 2 * 1024 * 1024, "函数范围异常");
                    int length = checked((int)(end - start));
                    int raw = Offset(start, length, true);
                    Decoder decoder = Decoder.Create(64, new ByteArrayCodeReader(Data, raw, length));
                    decoder.IP = start;
                    List<Instruction> result = new List<Instruction>();
                    while (decoder.IP < end)
                    {
                        Instruction instruction = decoder.Decode();
                        Need(instruction.Code != Code.INVALID && decoder.IP <= end, "函数包含无法解码的指令");
                        result.Add(instruction);
                    }
                    return result;
                }
            }
            throw new InvalidOperationException("目标位置没有对应的函数边界");
        }

        internal string AsciiAt(ulong rva, int max)
        {
            int offset = Offset(rva, 1, false), end = offset;
            while (end < Data.Length && end - offset < max && Data[end] >= 32 && Data[end] < 127) end++;
            return System.Text.Encoding.ASCII.GetString(Data, offset, end - offset);
        }

        private ushort U16(int p) { Need(p >= 0 && (long)p + 2 <= Data.Length, "PE 读取越界"); return BitConverter.ToUInt16(Data, p); }
        private uint U32(int p) { Need(p >= 0 && (long)p + 4 <= Data.Length, "PE 读取越界"); return BitConverter.ToUInt32(Data, p); }
        internal static void Need(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    }
}
