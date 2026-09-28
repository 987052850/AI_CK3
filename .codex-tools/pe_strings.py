import argparse
import pathlib


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("binary", type=pathlib.Path)
    parser.add_argument("offset", type=lambda value: int(value, 0))
    parser.add_argument("size", type=lambda value: int(value, 0))
    parser.add_argument("minimum", nargs="?", type=int, default=4)
    args = parser.parse_args()

    with args.binary.open("rb") as stream:
        stream.seek(args.offset)
        data = stream.read(args.size)

    start = None
    for index, value in enumerate(data + b"\x00"):
        printable = 0x20 <= value < 0x7f or value in (9, 10, 13)
        if printable and start is None:
            start = index
        elif not printable and start is not None:
            if index - start >= args.minimum:
                text = data[start:index].decode("ascii", errors="replace")
                print(f"{args.offset + start:#010x}  {text!r}")
            start = None


if __name__ == "__main__":
    main()
