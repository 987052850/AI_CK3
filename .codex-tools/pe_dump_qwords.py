import argparse
import pathlib

import pefile


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("binary", type=pathlib.Path)
    parser.add_argument("address", type=lambda value: int(value, 0))
    parser.add_argument("count", type=int)
    args = parser.parse_args()

    pe = pefile.PE(str(args.binary), fast_load=True)
    image_base = pe.OPTIONAL_HEADER.ImageBase
    offset = pe.get_offset_from_rva(args.address - image_base)
    with args.binary.open("rb") as stream:
        stream.seek(offset)
        data = stream.read(args.count * 8)

    for index in range(args.count):
        start = index * 8
        value = int.from_bytes(data[start:start + 8], "little")
        print(f"{args.address + start:#012x}  {value:#012x}")


if __name__ == "__main__":
    main()
