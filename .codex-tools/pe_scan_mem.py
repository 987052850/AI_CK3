import argparse
import pathlib
import sys

sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent))

import pefile
from capstone import Cs, CS_ARCH_X86, CS_MODE_64, CS_OP_MEM


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("binary", type=pathlib.Path)
    parser.add_argument("displacement", type=lambda value: int(value, 0))
    parser.add_argument("start", type=lambda value: int(value, 0))
    parser.add_argument("end", type=lambda value: int(value, 0))
    args = parser.parse_args()

    pe = pefile.PE(str(args.binary), fast_load=True)
    image_base = pe.OPTIONAL_HEADER.ImageBase
    offset = pe.get_offset_from_rva(args.start - image_base)
    with args.binary.open("rb") as stream:
        stream.seek(offset)
        code = stream.read(args.end - args.start)

    disassembler = Cs(CS_ARCH_X86, CS_MODE_64)
    disassembler.detail = True
    for instruction in disassembler.disasm(code, args.start):
        if any(
            operand.type == CS_OP_MEM and operand.mem.disp == args.displacement
            for operand in instruction.operands
        ):
            print(f"{instruction.address:#012x}  {instruction.mnemonic:<8} {instruction.op_str}")


if __name__ == "__main__":
    main()
