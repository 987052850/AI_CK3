import argparse
import pathlib
import sys

sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent))

import pefile
from capstone import Cs, CS_ARCH_X86, CS_MODE_64, CS_OP_MEM, x86_const


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("binary", type=pathlib.Path)
    parser.add_argument("target", type=lambda value: int(value, 0))
    parser.add_argument("target_end", nargs="?", type=lambda value: int(value, 0))
    args = parser.parse_args()

    pe = pefile.PE(str(args.binary), fast_load=True)
    image_base = pe.OPTIONAL_HEADER.ImageBase
    disassembler = Cs(CS_ARCH_X86, CS_MODE_64)
    disassembler.detail = True

    with args.binary.open("rb") as stream:
        for section in pe.sections:
            if not section.Characteristics & 0x20000000:
                continue
            stream.seek(section.PointerToRawData)
            code = stream.read(section.SizeOfRawData)
            address = image_base + section.VirtualAddress
            for instruction in disassembler.disasm(code, address):
                for operand in instruction.operands:
                    if operand.type != CS_OP_MEM or operand.mem.base != x86_const.X86_REG_RIP:
                        continue
                    referenced = instruction.address + instruction.size + operand.mem.disp
                    target_end = args.target_end if args.target_end is not None else args.target + 1
                    if args.target <= referenced < target_end:
                        print(
                            f"{instruction.address:#012x}  "
                            f"{instruction.mnemonic:<8} {instruction.op_str} -> {referenced:#012x}"
                        )


if __name__ == "__main__":
    main()
