from PIL import Image, ImageDraw, ImageFilter

S = 1024
BLUE = (99, 126, 255)
VIOLET = (139, 92, 246)
CORAL = (255, 116, 137)
INK = (37, 44, 82)
PAPER = (248, 250, 255)


def lerp(a, b, t):
    return tuple(round(a[i] + (b[i] - a[i]) * t) for i in range(3))


def rounded_mask(size, radius, inset=0):
    mask = Image.new("L", (size, size), 0)
    d = ImageDraw.Draw(mask)
    d.rounded_rectangle(
        (inset, inset, size - 1 - inset, size - 1 - inset),
        radius=radius,
        fill=255,
    )
    return mask


# --- rounded-square shadow -------------------------------------------------
shadow = Image.new("RGBA", (S, S), (0, 0, 0, 0))
sd = ImageDraw.Draw(shadow)
sd.rounded_rectangle((54, 78, 970, 994), radius=214, fill=(2, 5, 16, 135))
shadow = shadow.filter(ImageFilter.GaussianBlur(42))

# --- gradient tile ---------------------------------------------------------
gradient = Image.new("RGB", (S, S), BLUE)
gd = ImageDraw.Draw(gradient)
for y in range(S):
    t = y / (S - 1)
    gd.line((0, y, S, y), fill=lerp(BLUE, VIOLET, t))
# soft diagonal sheen
sheen = Image.new("RGBA", (S, S), (0, 0, 0, 0))
sh = ImageDraw.Draw(sheen)
sh.polygon([(0, 0), (720, 0), (210, 1024), (0, 1024)], fill=(255, 255, 255, 17))
sheen = sheen.filter(ImageFilter.GaussianBlur(3))

tile = Image.new("RGBA", (S, S), (0, 0, 0, 0))
tile.alpha_composite(gradient.convert("RGBA"))
tile.alpha_composite(sheen)
tile.putalpha(rounded_mask(S, 216, 42))

# subtle inner top highlight
highlight = Image.new("RGBA", (S, S), (0, 0, 0, 0))
hd = ImageDraw.Draw(highlight)
hd.arc((76, 76, 948, 948), 188, 352, fill=(255, 255, 255, 90), width=3)
highlight.putalpha(Image.composite(highlight.getchannel("A"), Image.new("L", (S, S), 0), rounded_mask(S, 216, 42)))

canvas = Image.new("RGBA", (S, S), (0, 0, 0, 0))
canvas.alpha_composite(shadow)
canvas.alpha_composite(tile)
canvas.alpha_composite(highlight)

# --- keyboard glyph --------------------------------------------------------
keyboard = Image.new("RGBA", (S, S), (0, 0, 0, 0))
kd = ImageDraw.Draw(keyboard)
# soft key shadow
kd.rounded_rectangle((196, 318, 846, 700), radius=74, fill=(15, 19, 44, 90))
kd.rounded_rectangle((188, 294, 838, 676), radius=74, fill=PAPER)

# keys
key_y = 344
key_w, key_h, gap = 78, 50, 17
start_x = 252
for row in range(2):
    y = key_y + row * 68
    for col in range(6):
        x = start_x + col * (key_w + gap)
        fill = CORAL if (row == 1 and col == 5) else INK
        kd.rounded_rectangle((x, y, x + key_w, y + key_h), radius=15, fill=fill)

# bottom row: modifiers + spacebar + coral accent
bottom_y = 480
kd.rounded_rectangle((242, bottom_y, 332, bottom_y + 48), radius=15, fill=INK)
kd.rounded_rectangle((350, bottom_y, 684, bottom_y + 48), radius=15, fill=INK)
kd.rounded_rectangle((702, bottom_y, 784, bottom_y + 48), radius=15, fill=INK)
# tiny key highlight on coral key
kd.rounded_rectangle((786, 420, 810, 430), radius=5, fill=(255, 255, 255, 175))

canvas.alpha_composite(keyboard)

# --- export -----------------------------------------------------------------
canvas.save("app_icon_1024.png")
canvas.resize((256, 256), Image.LANCZOS).save("app_icon_256.png")
canvas.save(
    "app.ico",
    sizes=[(16, 16), (20, 20), (24, 24), (32, 32), (40, 40), (48, 48), (64, 64), (128, 128), (256, 256)],
)
print("wrote app.ico")
