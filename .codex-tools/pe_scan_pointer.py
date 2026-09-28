import argparse
import pathlib


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("binary", type=pathlib.Path)
    parser.add_argument("target", type=lambda value: int(value, 0))
    args = parser.parse_args()

    needle = args.target.to_bytes(8, "little")
    data = args.binary.read_bytes()
    start = 0
    while True:
        offset = data.find(needle, start)
        if offset < 0:
            break
        print(f"{offset:#010x}")
        start = offset + 1


if __name__ == "__main__":
    main()
