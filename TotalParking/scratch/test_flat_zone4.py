from PIL import Image, ImageDraw
import os

img_path = 'TotalParking/Images/zones_map.jpeg'
img = Image.open(img_path)

container_w = 1016
container_h = 781

disp_w = container_w
disp_h = int(img.height * (container_w / img.width))
pad_y = int((container_h - disp_h) / 2.0)

img_resized = img.resize((disp_w, disp_h), Image.Resampling.LANCZOS)
canvas = Image.new('RGB', (container_w, container_h), (15, 23, 42))
canvas.paste(img_resized, (0, pad_y))

# Draw Zone 3 and Candidate Zone 4 (flat bottom at Y = 390)
draw = ImageDraw.Draw(canvas)
overlay = Image.new('RGBA', canvas.size, (0, 0, 0, 0))
draw_overlay = ImageDraw.Draw(overlay)

# Zone 3 optimized coordinates
pts3 = [(57,152), (185,46), (343,45), (308,248), (278,293), (308,390)]

# Zone 4 coordinates with flat bottom at Y = 390
pts4 = [
    (343, 45),   # top-left
    (571, 45),   # top-right
    (571, 390),  # bottom-right
    (412, 390),  # middle bottom 1
    (412, 390),  # middle bottom 2
    (372, 390),  # bottom-left 1
    (372, 390),  # bottom-left 2
    (308, 390),  # shared bottom-right of Zone 3
    (278, 293),  # shared bend 2
    (308, 248)   # shared bend 1
]

draw_overlay.polygon(pts3, fill=(249, 115, 22, 100), outline=(249, 115, 22, 255))
draw_overlay.polygon(pts4, fill=(168, 85, 247, 100), outline=(168, 85, 247, 255))

canvas_rgba = canvas.convert('RGBA')
final_img = Image.alpha_composite(canvas_rgba, overlay)
os.makedirs('TotalParking/scratch', exist_ok=True)
final_img.save('TotalParking/scratch/zone4_flat_verify.png')
print("Successfully generated zone4_flat_verify.png")
