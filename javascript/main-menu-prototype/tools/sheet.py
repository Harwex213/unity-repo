"""Joins the frames of art/shots into one contact sheet. Usage: sheet.py out.png cols width [names]"""
import sys, glob
from PIL import Image
out, cols, width = sys.argv[1], int(sys.argv[2]), int(sys.argv[3])
names = sys.argv[4:] or sorted(glob.glob("art/shots/t*.png"))
images = [Image.open(n).convert("RGB") for n in names]
h = round(width * images[0].height / images[0].width)
rows = (len(images) + cols - 1) // cols
sheet = Image.new("RGB", (cols * width, rows * h))
for i, im in enumerate(images):
    sheet.paste(im.resize((width, h), Image.LANCZOS), (i % cols * width, i // cols * h))
sheet.save(out)
