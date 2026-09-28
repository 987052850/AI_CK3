import argparse
import pathlib
import sys

sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent))

import pefile
from capstone import Cs, CS_ARCH_X86, CS_MODE_64


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("binary", type=pathlib.Path)
    parser.add_argument("address", type=lambda value: int(value, 0))
    parser.add_argument("size", type=lambda value: int(value, 0))
    args = parser.parse_args()

    pe = pefile.PE(str(args.binary), fast_load=True)
    image_base = pe.OPTIONAL_HEADER.ImageBase
    rva = args.address - image_base
    offset = pe.get_offset_from_rva(rva)
    with args.binary.open("rb") as stream:
        stream.seek(offset)
        code = stream.read(args.size)

    disassembler = Cs(CS_ARCH_X86, CS_MODE_64)
    disassembler.detail = False
    for instruction in disassembler.disasm(code, args.address):
        raw = instruction.bytes.hex(" ")
        print(f"{instruction.address:#012x}  {raw:<31}  {instruction.mnemonic:<8} {instruction.op_str}")


if __name__ == "__main__":
    main()
