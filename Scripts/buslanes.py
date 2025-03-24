"""
Program to find and identify bus lanes on an image in order to add bay cards with current bay information.

Each bus lane is a bar with either red, green or blue color to identify lane A, B or C respectively.

Created by: Jacob D. Walton (Jacob-Walton)
Date: 01/03/2025
Version: 1.0

Acknowledgements:
- https://github.com/ToastedCheesyBeans for the initial buslanes image.
- https://www.runshaw.ac.uk for the bus lane image that this was image was built from.
"""

import cv2
import numpy as np
import matplotlib.pyplot as plt
from PIL import Image, ImageDraw, ImageFont
import pytesseract
import os

# Load the image
image = cv2.imread('buslanes.png')

# Convert the image to RGB (OpenCV uses BGR)
image = cv2.cvtColor(image, cv2.COLOR_BGR2RGB)

def locate_bounds(lower, upper):
    # Create a mask
    mask = cv2.inRange(image, lower, upper)

    # Find contours
    contours, _ = cv2.findContours(mask, cv2.RETR_EXTERNAL, cv2.CHAIN_APPROX_SIMPLE)

    # Get the bounding box of the contours
    bounding_boxes = [cv2.boundingRect(contour) for contour in contours]
        
    return bounding_boxes

def draw_alternate_border_radius_rect(draw, xy, radius, fill):
    x0, y0, x1, y1 = xy
    width = x1 - x0
    height = y1 - y0
    
    # Create a new transparent image for rectangle
    rect_img = Image.new('RGBA', (width, height), (0, 0, 0, 0))
    rect_draw = ImageDraw.Draw(rect_img)
    
    # Draw with rounded corners
    rect_draw.rounded_rectangle([(0, 0), (width, height)], radius, fill=fill)
    
    # Create masks for corners which will be squared
    corner_size = radius
    
    # Top-left corner mask
    tl_mask = Image.new('L', (width, height), 0)
    tl_draw = ImageDraw.Draw(tl_mask)
    tl_draw.rectangle([(0, 0), (corner_size, corner_size)], fill=255)
    
    # Bottom-right corner mask
    br_mask = Image.new('L', (width, height), 0)
    br_draw = ImageDraw.Draw(br_mask)
    br_draw.rectangle([(width - corner_size, height - corner_size), 
                      (width, height)], fill=255)
    
    # Apply square corners by copying from square rectangle
    square_rect = Image.new('RGBA', (width, height), fill)
    rect_img.paste(square_rect, (0, 0), mask=tl_mask)
    rect_img.paste(square_rect, (0, 0), mask=br_mask)
    
    # Paste the rectangle onto the image
    pil_image.paste(rect_img, (x0, y0), mask=rect_img)

# Define the lower and upper bounds for each color
lower_bounds = [(255, 0, 0), (0, 255, 0), (0, 0, 255)]
upper_bounds = [(255, 0, 0), (0, 255, 0), (0, 0, 255)]

# Get the bounding boxes for each color
bounding_boxes = [locate_bounds(lower, upper) for lower, upper in zip(lower_bounds, upper_bounds)]
        
# Output to console all of the bounding boxes
print(bounding_boxes)

# Fill the boxes with (210, 217, 227), the color of the background
for i, boxes in enumerate(bounding_boxes):
    for x, y, w, h in boxes:
        image[y:y+h, x:x+w] = [210, 217, 227]
        
# Convert the image to a PIL image
pil_image = Image.fromarray(image)
draw = ImageDraw.Draw(pil_image)

# Try to load font
try:
    font = ImageFont.truetype("fonts/Ubuntu-Bold.ttf", 16)
except IOError:
    font = ImageFont.load_default()
    
# Fill bus map with A1-16, B1-16, C1-16 with their values as ""
bus_map = {f"{chr(65 + i)}{j}": "809" for i in range(3) for j in range(1, 17)}

lane_prefixes = ["A", "B", "C"]

for i, boxes in enumerate(bounding_boxes):
    if boxes:
        x, y, w, h = boxes[0]
        prefix = lane_prefixes[i]
        
        # Set card color
        card_color = (231, 48, 42)
        
        # Dimensions
        section_width = w // 16
        rect_wdith = 70
        rect_height = 50
        
        for j in range(16):
            # Calculate position
            card_x = x + (j * section_width) + (section_width // 2)
            card_y = y + (h // 2)
            
            # Calculate rectangle position
            rect_left = card_x - (rect_wdith // 2)
            rect_top = card_y - (rect_height // 2)
            rect_right = rect_left + rect_wdith
            rect_bottom = rect_top + rect_height
            
            # Draw rectangle
            border_radius = 10
            
            draw_alternate_border_radius_rect(draw,
                            [rect_left, rect_top, rect_right, rect_bottom], 
                            border_radius, 
                            card_color)
            
            # Add bay number
            bay_number = f"{prefix}{16 - j}"
            service = bus_map.get(bay_number, "")
            
            # Get text heights
            font_height = font.getbbox("Aj")[3] 
            total_text_height = font_height * 2 if service else font_height
            
            # Calculate vertical positions to center all text
            text_start_y = rect_top + (rect_height - total_text_height) // 2
            
            # Calculate and draw bay number
            text_width = draw.textlength(bay_number, font=font)
            text_x = card_x - (text_width // 2)
            draw.text((text_x, text_start_y), bay_number, fill=(255, 255, 255), font=font)
            
            # Add service number if available
            if service:
                service_width = draw.textlength(service, font=font)
                service_x = card_x - (service_width // 2)
                draw.text((service_x, text_start_y + font_height), service, fill=(255, 255, 255), font=font)
                
# Convert back to numpy array
image = np.array(pil_image)

# Display the image
plt.figure(figsize=(15, 10))
plt.imshow(image)
plt.axis('off')
plt.show()