"""Small-scope PE string-xref helper for CK3 evidence work.

This intentionally scans only RIP-relative displacements in .text and disassembles
the exception-table function that contains a confirmed reference. It is not a
whole-program disassembler.
"""

from __future__ import annotations

import argparse
import bisect
import struct
from pathlib import Path

import pefile
from capstone import CS_ARCH_X86, CS_MODE_64, Cs
from capstone.x86 import X86_OP_MEM, X86_REG_RIP


def raw_to_rva(pe: pefile.PE, offset: int) -> int | None:
    for section in pe.sections:
        start = section.PointerToRawData
        end = start + section.SizeOfRawData
        if start <= offset < end:
            return section.VirtualAddress + offset - start
    return None


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("exe", type=Path)
    parser.add_argument("targets", nargs="*")
    parser.add_argument("--rva", action="append", default=[], help="Function RVA/address to dump")
    parser.add_argument("--call-target", action="append", default=[], help="Find direct callers of RVA/address")
    parser.add_argument("--raw", action="append", default=[], help="Raw RVA/address:size disassembly")
    parser.add_argument("--output", type=Path)
    args = parser.parse_args()

    data = args.exe.read_bytes()
    pe = pefile.PE(data=data, fast_load=False)
    base = pe.OPTIONAL_HEADER.ImageBase
    text_section = next(s for s in pe.sections if s.Name.rstrip(b"\0") == b".text")
    text = text_section.get_data()
    text_rva = text_section.VirtualAddress

    target_labels: dict[int, list[str]] = {}
    lines: list[str] = []
    for label in args.targets:
        needle = label.encode("utf-8")
        cursor = 0
        found = False
        while True:
            offset = data.find(needle, cursor)
            if offset < 0:
                break
            found = True
            rva = raw_to_rva(pe, offset)
            if rva is not None:
                target_labels.setdefault(base + rva, []).append(label)
                lines.append(f"STRING {label!r} raw=0x{offset:x} rva=0x{rva:x} va=0x{base+rva:x}")
            cursor = offset + 1
        if not found:
            lines.append(f"STRING {label!r} NOT FOUND")

    functions = sorted(
        (entry.struct.BeginAddress, entry.struct.EndAddress)
        for entry in pe.DIRECTORY_ENTRY_EXCEPTION
        if entry.struct.BeginAddress < entry.struct.EndAddress
    )
    starts = [item[0] for item in functions]

    candidates: dict[tuple[int, int], set[tuple[int, str]]] = {}
    targets = set(target_labels)
    if targets:
        for index in range(0, len(text) - 4):
            disp = struct.unpack_from("<i", text, index)[0]
            resolved = base + text_rva + index + 4 + disp
            if resolved not in targets:
                continue
            instruction_rva = text_rva + max(0, index - 7)
            function_index = bisect.bisect_right(starts, instruction_rva) - 1
            if function_index < 0:
                continue
            begin, end = functions[function_index]
            if not (begin <= instruction_rva < end):
                continue
            for label in target_labels[resolved]:
                candidates.setdefault((begin, end), set()).add((resolved, label))

    for value in args.rva:
        address = int(value, 0)
        rva = address - base if address >= base else address
        function_index = bisect.bisect_right(starts, rva) - 1
        if function_index >= 0:
            begin, end = functions[function_index]
            if begin <= rva < end:
                candidates.setdefault((begin, end), set()).add((base + rva, "requested RVA"))

    call_targets = set()
    for value in args.call_target:
        address = int(value, 0)
        call_targets.add(address if address >= base else base + address)
    if call_targets:
        for index in range(0, len(text) - 5):
            if text[index] != 0xE8:
                continue
            disp = struct.unpack_from("<i", text, index + 1)[0]
            resolved = base + text_rva + index + 5 + disp
            if resolved not in call_targets:
                continue
            rva = text_rva + index
            function_index = bisect.bisect_right(starts, rva) - 1
            if function_index >= 0:
                begin, end = functions[function_index]
                if begin <= rva < end:
                    candidates.setdefault((begin, end), set()).add((resolved, "direct call target"))

    md = Cs(CS_ARCH_X86, CS_MODE_64)
    md.detail = True
    for (begin, end), refs in sorted(candidates.items()):
        size = end - begin
        if size > 0x20000:
            lines.append(f"\nFUNCTION 0x{base+begin:x}-0x{base+end:x} size=0x{size:x} SKIPPED_TOO_LARGE")
            continue
        code = pe.get_data(begin, size)
        decoded = list(md.disasm(code, base + begin))
        confirmed: list[tuple[int, str]] = []
        for insn in decoded:
            for operand in insn.operands:
                if operand.type != X86_OP_MEM or operand.mem.base != X86_REG_RIP:
                    continue
                resolved = insn.address + insn.size + operand.mem.disp
                if resolved in target_labels:
                    confirmed.extend((resolved, label) for label in target_labels[resolved])
        requested = any(label in ("requested RVA", "direct call target") for _, label in refs)
        if not confirmed and not requested:
            continue
        lines.append(f"\nFUNCTION 0x{base+begin:x}-0x{base+end:x} rva=0x{begin:x} size=0x{size:x}")
        for resolved, label in sorted(set(confirmed)):
            lines.append(f"  REFERENCES {label!r} at 0x{resolved:x}")
        if requested:
            for resolved, label in sorted(refs):
                if label in ("requested RVA", "direct call target"):
                    lines.append(f"  {label.upper()} 0x{resolved:x}")
        for insn in decoded:
            lines.append(f"  {insn.address:016x}  {insn.mnemonic:8s} {insn.op_str}")

    for spec in args.raw:
        value, size_text = spec.split(":", 1)
        address = int(value, 0)
        rva = address - base if address >= base else address
        size = int(size_text, 0)
        lines.append(f"\nRAW 0x{base+rva:x} size=0x{size:x}")
        for insn in md.disasm(pe.get_data(rva, size), base + rva):
            lines.append(f"  {insn.address:016x}  {insn.mnemonic:8s} {insn.op_str}")

    result = "\n".join(lines) + "\n"
    if args.output:
        args.output.parent.mkdir(parents=True, exist_ok=True)
        args.output.write_text(result, encoding="utf-8")
    else:
        print(result, end="")


if __name__ == "__main__":
    main()
