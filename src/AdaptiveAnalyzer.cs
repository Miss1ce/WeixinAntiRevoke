using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using Iced.Intel;

namespace WeixinAntiRevoke
{
    internal sealed class PatchEdit
    {
        public int Offset;
        public byte[] Before, After;
        public string Reason;
    }

    internal sealed class AdaptivePlan
    {
        public string OriginalHash, PromptHash, LegacyHash, Report;
        public PatchEdit[] PromptEdits, LegacyEdits;
        public string PromptFailure;
        public byte[] Build(byte[] source, bool prompt)
        {
            PeImage.Need(PatchEngine.ComputeSha256(source) == OriginalHash, "原文件在分析后发生变化");
            PatchEdit[] edits = prompt ? PromptEdits : LegacyEdits;
            PeImage.Need(edits != null && edits.Length > 0, prompt ? PromptFailure : "不带提示模式不可用");
            byte[] result = (byte[])source.Clone();
            HashSet<int> touched = new HashSet<int>();
            foreach (PatchEdit e in edits)
            {
                PeImage.Need(e.Before.Length == e.After.Length && e.Offset >= 0 && (long)e.Offset + e.Before.Length <= result.Length, "补丁范围无效");
                for (int n = 0; n < e.Before.Length; n++)
                {
                    PeImage.Need(touched.Add(e.Offset + n) && result[e.Offset + n] == e.Before[n], "补丁重叠或原字节发生变化");
                    result[e.Offset + n] = e.After[n];
                }
            }
            return result;
        }
    }

    internal static class AdaptiveAnalyzer
    {
        private const string LegacyPattern = "84C00F85????FFFF488B8D????0000E8????????90488986????????4C";
        private const string MessagePattern = "488D55??4531C0E8????????90488B9D????00004885DB";
        private const string IdPattern = "48C1E0204909??C644242801C644242000";

        internal static AdaptivePlan Analyze(byte[] data)
        {
            PeImage pe = new PeImage(data);
            StringBuilder report = new StringBuilder("本地结构分析 v1（实验性；未进行消息收发验证）\r\n");
            List<long> legacy = BytePattern.FindAll(data, Pattern(LegacyPattern));
            PeImage.Need(legacy.Count == 1, "撤回 ID 解析特征缺失或不唯一");
            int legacyOffset = checked((int)legacy[0]);
            List<Instruction> parser = pe.FunctionAt(pe.Rva(legacyOffset));
            int li = Index(parser, pe.Rva(legacyOffset));
            PeImage.Need(li >= 0 && li + 5 < parser.Count, "解析位置不在指令边界上");
            Instruction store = parser[li + 5];
            PeImage.Need(store.IP == pe.Rva(legacyOffset + 21) && store.Mnemonic == Mnemonic.Mov && store.Op0Kind == OpKind.Memory && store.Op1Register == Register.RAX && store.MemoryBase == Register.RSI && store.MemoryIndex == Register.None && store.Length == 7, "撤回 ID 写入指令结构改变");
            ulong idField = store.MemoryDisplacement64;
            PeImage.Need(idField >= 8 && idField <= 0x2000 && idField % 8 == 0, "撤回 ID 字段范围异常");
            PeImage.Need(HasString(pe, parser, 0, parser.Count, "revokemsg") && HasString(pe, parser, Math.Max(0, li - 12), li, "newmsgid"), "未确认 revokemsg / newmsgid 解析上下文");
            PeImage.Need(parser[li + 1].FlowControl == FlowControl.ConditionalBranch && parser[li + 2].Mnemonic == Mnemonic.Mov && parser[li + 2].Op0Register == Register.RCX && parser[li + 3].FlowControl == FlowControl.Call && parser[li + 4].Mnemonic == Mnemonic.Nop, "撤回解析调用结构改变");
            pe.Offset(parser[li + 3].NearBranchTarget, 1, true);
            RejectInteriorBranches(parser, store.IP, store.NextIP);
            byte[] legacyAfter = Slice(data, legacyOffset + 21, 7);
            legacyAfter[1] = 0x29;
            AdaptivePlan plan = new AdaptivePlan { OriginalHash = PatchEngine.ComputeSha256(data), LegacyEdits = new[] { Edit(data, legacyOffset + 21, legacyAfter, "撤回 ID 写入改为相减（已关联 newmsgid）") } };
            report.AppendFormat("撤回对象 ID 字段：+0x{0:X}；无提示修改位置：0x{1:X}\r\n", idField, legacyOffset + 21);
            try
            {
                List<long> ids = BytePattern.FindAll(data, Pattern(IdPattern));
                PeImage.Need(ids.Count == 1, "提示 ID 特征缺失或不唯一");
                int idOffset = checked((int)ids[0]);
                List<Instruction> idFunction = pe.FunctionAt(pe.Rva(idOffset));
                int ii = Index(idFunction, pe.Rva(idOffset));
                PeImage.Need(ii >= 0 && ii + 3 < idFunction.Count && idFunction.Count < 80, "提示 ID 函数结构改变");
                Instruction flag = idFunction[ii + 3];
                PeImage.Need(flag.IP == pe.Rva(idOffset + 12) && flag.Mnemonic == Mnemonic.Mov && flag.Op0Kind == OpKind.Memory && flag.MemoryBase == Register.RSP && flag.MemoryIndex == Register.None && flag.MemoryDisplacement64 == 0x20 && flag.Op1Kind == OpKind.Immediate8 && flag.Immediate8 == 0 && flag.Length == 5, "提示 ID 参数指令不符合预期");
                int idCalls = 0;
                foreach (Instruction ins in idFunction) if (ins.FlowControl == FlowControl.Call) { pe.Offset(ins.NearBranchTarget, 1, true); idCalls++; }
                PeImage.Need(idCalls == 1 && idFunction[idFunction.Count - 1].Mnemonic == Mnemonic.Ret, "提示 ID 包装函数无法确认");
                RejectInteriorBranches(idFunction, flag.IP, flag.NextIP);
                ulong wrapper = idFunction[0].IP;
                List<long> messages = BytePattern.FindAll(data, Pattern(MessagePattern));
                PeImage.Need(messages.Count > 0 && messages.Count <= 16, "消息候选数量异常");
                List<PatchEdit> accepted = new List<PatchEdit>();
                report.AppendFormat("消息字节候选：{0} 处\r\n", messages.Count);
                foreach (long match in messages)
                {
                    int offset = checked((int)match);
                    try
                    {
                        PatchEdit edit = AnalyzeMessage(pe, offset, idField, wrapper);
                        accepted.Add(edit);
                        report.AppendFormat("接受 0x{0:X}：{1}\r\n", offset, edit.Reason);
                    }
                    catch (InvalidOperationException ex) { report.AppendFormat("排除 0x{0:X}：{1}\r\n", offset, ex.Message); }
                }
                // This recognizer understands one revoke handler. A new topology must be analyzed explicitly.
                PeImage.Need(accepted.Count == 1, "无法唯一确认撤回消息处理函数");
                byte[] flagAfter = Slice(data, idOffset + 12, 5); flagAfter[4] = 1;
                accepted.Add(Edit(data, idOffset + 12, flagAfter, "允许提示使用新 ID"));
                plan.PromptEdits = accepted.ToArray();
                report.AppendFormat("提示 ID 参数：0x{0:X}；实际消息修改：1 处\r\n", idOffset + 16);
                plan.PromptHash = PatchEngine.ComputeSha256(plan.Build(data, true));
            }
            catch (InvalidOperationException ex)
            {
                plan.PromptFailure = "带提示结构未确认：" + ex.Message;
                report.AppendLine(plan.PromptFailure);
            }
            plan.LegacyHash = PatchEngine.ComputeSha256(plan.Build(data, false));
            report.AppendLine("原始 SHA-256：" + plan.OriginalHash);
            report.AppendLine("不带提示 SHA-256：" + plan.LegacyHash);
            if (plan.PromptHash != null) report.AppendLine("带提示 SHA-256：" + plan.PromptHash);
            report.AppendLine("以上哈希记录推导结果，不代表实际消息行为已验证。");
            plan.Report = report.ToString();
            return plan;
        }

        private static PatchEdit AnalyzeMessage(PeImage pe, int offset, ulong idField, ulong wrapper)
        {
            List<Instruction> f = pe.FunctionAt(pe.Rva(offset));
            int mi = Index(f, pe.Rva(offset));
            PeImage.Need(mi >= 0 && mi + 2 < f.Count, "候选不在函数指令边界");
            PeImage.Need(f[mi].Mnemonic == Mnemonic.Lea && f[mi].Op0Register == Register.RDX && f[mi].MemoryBase == Register.RBP && f[mi + 1].Mnemonic == Mnemonic.Xor && f[mi + 1].Op0Register == Register.R8D && f[mi + 1].Op1Register == Register.R8D && f[mi + 2].FlowControl == FlowControl.Call && f[mi + 2].NextIP - f[mi + 1].IP == 8, "消息处理调用结构改变");
            pe.Offset(f[mi + 2].NearBranchTarget, 1, true);
            List<int> assignments = new List<int>();
            for (int n = 0; n + 1 < mi; n++)
            {
                Instruction load = f[n], save = f[n + 1];
                if (load.Mnemonic == Mnemonic.Mov && load.Op0Register == Register.RAX && load.Op1Kind == OpKind.Memory && load.MemoryIndex == Register.None && load.MemoryBase != Register.RBP && load.MemoryBase != Register.RSP && load.MemoryBase != Register.RIP && load.MemoryBase != Register.None && load.MemoryDisplacement64 == idField && save.Mnemonic == Mnemonic.Mov && save.Op0Kind == OpKind.Memory && save.MemoryBase == Register.RBP && save.MemoryIndex == Register.None && save.Op1Register == Register.RAX)
                    assignments.Add(n + 1);
            }
            PeImage.Need(assignments.Count == 1, "未找到唯一的撤回 ID 到局部消息对象的赋值");
            int assignment = assignments[0];
            ulong slot = f[assignment].MemoryDisplacement64;
            ulong frameSize = 0, frameBias = 0;
            foreach (Instruction ins in f.GetRange(0, Math.Min(15, f.Count)))
            {
                if (ins.Mnemonic == Mnemonic.Sub && ins.Op0Register == Register.RSP && ins.Op1Kind == OpKind.Immediate32to64) frameSize = ins.Immediate32;
                if (ins.Mnemonic == Mnemonic.Lea && ins.Op0Register == Register.RBP && ins.MemoryBase == Register.RSP) frameBias = ins.MemoryDisplacement64;
            }
            PeImage.Need(slot >= 8 && slot % 8 == 0 && frameSize > frameBias && slot + 8 < frameSize - frameBias && slot < int.MaxValue, "推导的 ID 栈位置不在本地栈帧范围");
            PeImage.Need(!Reachable(f, 0, mi, assignment), "ID 初始化未覆盖所有到达补丁的控制流");
            PeImage.Need(Reachable(f, assignment, mi, -1), "ID 初始化与补丁位置无可达路径");
            int insertion = -1;
            ulong objectBase = 0;
            for (int n = mi + 3; n < f.Count && f[n].IP - f[mi].IP < 1536; n++)
            {
                if (f[n].FlowControl != FlowControl.Call || n < 1) continue;
                Instruction arg = f[n - 1];
                if (arg.Mnemonic != Mnemonic.Lea || arg.Op0Register != Register.R8 || arg.MemoryBase != Register.RBP || arg.MemoryIndex != Register.None) continue;
                List<Instruction> helper = pe.FunctionAt(f[n].NearBranchTarget);
                if (helper[0].IP != f[n].NearBranchTarget || helper.Count > 250) continue;
                bool callsWrapper = false;
                foreach (Instruction ins in helper) if (ins.FlowControl == FlowControl.Call && ins.NearBranchTarget == wrapper) callsWrapper = true;
                if (!callsWrapper) continue;
                PeImage.Need(insertion == -1, "存在多个提示插入调用");
                insertion = n; objectBase = arg.MemoryDisplacement64;
            }
            PeImage.Need(insertion >= 0 && objectBase < slot && slot - objectBase < 0x800 && Reachable(f, mi, insertion, -1), "未确认局部消息对象与提示 ID 函数的调用关系");
            RejectInteriorBranches(f, f[mi + 1].IP, f[mi + 2].NextIP);
            byte[] replacement = new byte[] { 0x48, 0x83, 0x85, 0, 0, 0, 0, 1 };
            Buffer.BlockCopy(BitConverter.GetBytes((int)slot), 0, replacement, 3, 4);
            return Edit(pe.Data, offset + 4, replacement, string.Format("推导 ID=[rbp+0x{0:X}]，消息对象=[rbp+0x{1:X}]，已核对初始化路径与提示插入调用", slot, objectBase));
        }

        private static bool Reachable(List<Instruction> f, int start, int target, int excluded)
        {
            Dictionary<ulong, int> indices = new Dictionary<ulong, int>();
            for (int n = 0; n < f.Count; n++) indices[f[n].IP] = n;
            Queue<int> pending = new Queue<int>(); HashSet<int> seen = new HashSet<int>(); pending.Enqueue(start);
            while (pending.Count > 0)
            {
                int n = pending.Dequeue();
                if (n == excluded || n < 0 || n >= f.Count || !seen.Add(n)) continue;
                if (n == target) return true;
                Instruction i = f[n];
                if (i.FlowControl == FlowControl.IndirectBranch) throw new InvalidOperationException("目标控制流包含无法推导的间接跳转");
                if (i.FlowControl == FlowControl.ConditionalBranch || i.FlowControl == FlowControl.UnconditionalBranch)
                { int next; if (indices.TryGetValue(i.NearBranchTarget, out next)) pending.Enqueue(next); }
                if (i.FlowControl != FlowControl.UnconditionalBranch && i.FlowControl != FlowControl.Return && i.FlowControl != FlowControl.Exception) pending.Enqueue(n + 1);
            }
            return false;
        }

        private static bool HasString(PeImage pe, List<Instruction> f, int start, int end, string value)
        {
            for (int n = start; n < end; n++) if (f[n].Mnemonic == Mnemonic.Lea && f[n].IsIPRelativeMemoryOperand)
            { try { if (pe.AsciiAt(f[n].IPRelativeMemoryAddress, value.Length + 1) == value) return true; } catch (InvalidOperationException) {} }
            return false;
        }
        private static void RejectInteriorBranches(List<Instruction> f, ulong start, ulong end)
        {
            foreach (Instruction ins in f) if ((ins.FlowControl == FlowControl.ConditionalBranch || ins.FlowControl == FlowControl.UnconditionalBranch) && ins.NearBranchTarget > start && ins.NearBranchTarget < end) throw new InvalidOperationException("分支进入补丁内部，无法替换");
        }
        private static int Index(List<Instruction> f, ulong ip) { for (int n = 0; n < f.Count; n++) if (f[n].IP == ip) return n; return -1; }
        private static PatchEdit Edit(byte[] data, int offset, byte[] after, string reason) { return new PatchEdit { Offset = offset, Before = Slice(data, offset, after.Length), After = after, Reason = reason }; }
        private static byte[] Slice(byte[] data, int offset, int length) { byte[] bytes = new byte[length]; Buffer.BlockCopy(data, offset, bytes, 0, length); return bytes; }
        private static int[] Pattern(string text) { int[] p = new int[text.Length / 2]; for (int n = 0; n < p.Length; n++) p[n] = text.Substring(n * 2, 2) == "??" ? -1 : Convert.ToInt32(text.Substring(n * 2, 2), 16); return p; }
    }
}
