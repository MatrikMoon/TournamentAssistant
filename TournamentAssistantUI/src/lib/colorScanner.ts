import Color from "color";

/*
 * Houses functions to help with scanning for colors during stream sync
 * General overview: We will search pixels from left to right, attempting
 * to find borders between the four colors we've designated. When we find
 * the *second* color we're looking for, we will look backwards (jumping
 * 5 pixels to counteract blur between squares) to see if the color of the
 * previous block matches the first color we're looking for, and we'll
 * repeat this process until we've found all three borders in one row
 * of pixels. The findBlockSize function will look either down and backwards
 * or down and forwards to judge the size of the color block, and if it is
 * at least 10, it will be a valid color block, and thus a valid border
 */

export type Point = { x: number, y: number };
export type Block = { x: number, y: number, width: number, height: number, color: Color };
export type ColorBar = { centerPoint: Point, blocks: Block[] };

export class ColorScanner {
    // Converting array location to X and Y
    public static xyFromArrayLocation(arrayLocation: number, imageWidth: number): Point {
        return { x: arrayLocation % imageWidth, y: (arrayLocation / imageWidth) | 0 }
    }

    // Converting X and Y to array location
    // NOTE: This DOES NOT take into account the fact that
    // a "pixel" is 4 numbers. You'll need to multiply by 4 if
    // you need the index for something like that
    public static arrayLocationFromXY(x: number, y: number, imageWidth: number) {
        return y * imageWidth + x;
    }

    // Returns true if the two provided colors are within `threshold` color values of each other
    // Stream encoders can tweak this, and rarely it's tweaked by a substantial amount
    // Old TA used a threshold of 50, which I'm going to try to use by default here
    private static matchesColorWithinThreshold(color1?: Color, color2?: Color, threshold: number = 20) {
        if (!color1 || !color2) {
            return false;
        }

        return Math.abs(color1.red() - color2.red()) < threshold &&
            Math.abs(color1.green() - color2.green()) < threshold &&
            Math.abs(color1.blue() - color2.blue()) < threshold;
    }

    // When we find a border, we'll want to look backwards and down
    // to see that the size of the color we're detecting is at least
    // 10 pixels by 10 pixels. May as well find the size of the block
    // while we're at it. Searches backwards and down by default
    private static findBlockSize(blockColor: Color, x: number, y: number, imageData: ImageData, forward: boolean = false) {
        // Result values
        let horizontalSize = 0;
        let verticalSize = 0;

        // --- Horizontal check --- //
        let startingPixel = this.arrayLocationFromXY(x, y, imageData.width);

        // Check horizontally until we hit an edge, or a different color
        let endingPixel = forward ? (y + 1) * imageData.width : y * imageData.width;

        for (
            let pixel = startingPixel;
            forward ? pixel < endingPixel : pixel > endingPixel;
            forward ? pixel++ : pixel--
        ) {
            const locationInArray = pixel * 4;
            const data = imageData!.data;
            const currentPixelColor = Color({
                r: data[locationInArray],
                g: data[locationInArray + 1],
                b: data[locationInArray + 2],
                alpha: data[locationInArray + 3],
            });

            // If the pixel matches the color we're looking for, the square is one pixel bigger
            if (this.matchesColorWithinThreshold(currentPixelColor, blockColor)) {
                horizontalSize++;
            }

            // If the pixel doesn't match the color we're looking for, we're done
            else {
                break;
            }
        }

        // --- Vertical check --- //
        startingPixel = this.arrayLocationFromXY(x, y, imageData.width);

        // Check vertically until we hit the bottom edge, or a different color.
        // Note that we're assuming it's the bottom edge here, because only
        // the horizontal check has to deal with "forward" and "backward,"
        // we're lucky enough here we just have "down"
        endingPixel = this.arrayLocationFromXY(x, imageData.height, imageData.width) + 1;

        for (
            let pixel = startingPixel;
            pixel < endingPixel;
            pixel += imageData.width
        ) {
            const locationInArray = pixel * 4;
            const data = imageData!.data;
            const currentPixelColor = Color({
                r: data[locationInArray],
                g: data[locationInArray + 1],
                b: data[locationInArray + 2],
                alpha: data[locationInArray + 3],
            });

            // If the pixel matches the color we're looking for, the square is one pixel bigger
            if (this.matchesColorWithinThreshold(currentPixelColor, blockColor)) {
                verticalSize++;
            }

            // If the pixel doesn't match the color we're looking for, we're done
            else {
                break;
            }
        }

        return { horizontalSize, verticalSize };
    }

    // This one takes the current coordinates, color, and last seen color
    // to determine if the current location is a border between colors
    private static isBorderBetweenColors(currentPixelColor: Color, jumpbackPixelColor: Color, currentLookingForColor: Color, jumpbackLookingForColor: Color, x: number, y: number, jumpbackDistance: number, imageData: ImageData) {

        // If the color of the current pixel matches the *second* color,
        // and the last pixel we saw matches the first color, then we've found the first border
        if (this.matchesColorWithinThreshold(currentPixelColor, currentLookingForColor) && this.matchesColorWithinThreshold(jumpbackPixelColor, jumpbackLookingForColor)) {

            // Check that the current block and last block meet the minimum size requirements
            const minimumBlockSize = 5;
            const currentBlockSize = this.findBlockSize(currentLookingForColor, x, y, imageData, true);
            const lastBlockSize = this.findBlockSize(jumpbackLookingForColor, x - jumpbackDistance, y, imageData);

            // console.log(`Block sizes: ${currentBlockSize.horizontalSize}, ${currentBlockSize.verticalSize} : ${lastBlockSize.horizontalSize}, ${lastBlockSize.verticalSize}`);

            // console.log(currentPixelColor.toString(), jumpbackPixelColor.toString());
            // console.log(currentLookingForColor.toString(), jumpbackLookingForColor.toString());

            // If both meet the minimum size requirements, we've found a valid border
            if (currentBlockSize.horizontalSize >= minimumBlockSize &&
                currentBlockSize.verticalSize >= minimumBlockSize &&
                lastBlockSize.horizontalSize >= minimumBlockSize &&
                lastBlockSize.verticalSize >= minimumBlockSize) {

                // console.log('Block sizes:', { currentBlockSize }, { lastBlockSize });
                return true;
            }
        }

        return false;
    }

    // Returns the center location of the provided sequence of colors
    // This assumes the image contains a sequence of four colored squares.
    // Making that assumption, it finds the first instance it can where
    // the four colors (at least 10 square pixels each) border each other
    // in order, then returns the location in the center of the rectangle
    public static getLocationOfSequence(color1: Color, color2: Color, color3: Color, color4: Color, imageData: ImageData): ColorBar | undefined {
        let jumpbackDistance = 5;
        let jumpbackPixelColor: Color | undefined;
        let bordersFound: Point[] = [];

        for (
            let pixel = 0;
            pixel < imageData!.width * imageData!.height;
            pixel++
        ) {
            const locationInArray = pixel * 4;
            const pixelCoordinates = this.xyFromArrayLocation(pixel, imageData!.width);

            const data = imageData!.data;
            const color = Color({
                r: data[locationInArray],
                g: data[locationInArray + 1],
                b: data[locationInArray + 2],
                alpha: data[locationInArray + 3],
            });

            // If we reach the end of a row, we should not
            // search backwards with the next pixel
            if (pixelCoordinates.x >= 5) {
                const fiveAgoLocation = this.arrayLocationFromXY(pixelCoordinates.x - 5, pixelCoordinates.y, imageData!.width) * 4;
                const fiveAgoColor = Color({
                    r: data[fiveAgoLocation],
                    g: data[fiveAgoLocation + 1],
                    b: data[fiveAgoLocation + 2],
                    alpha: data[fiveAgoLocation + 3],
                });
                jumpbackPixelColor = fiveAgoColor;
            }
            else {
                jumpbackPixelColor = undefined;
                bordersFound = [];
            }

            // If this isn't the first pixel in the row
            if (jumpbackPixelColor) {

                // If we haven't yet found a border, we're looking for the border between color1 and color2
                if (bordersFound.length === 0 && this.isBorderBetweenColors(color, jumpbackPixelColor, color2, color1, pixelCoordinates.x, pixelCoordinates.y, jumpbackDistance, imageData)) {
                    bordersFound.push(pixelCoordinates);
                }

                // If we've found one border, we're now looking for the border between color2 and color3
                else if (bordersFound.length === 1 && this.isBorderBetweenColors(color, jumpbackPixelColor, color3, color2, pixelCoordinates.x, pixelCoordinates.y, jumpbackDistance, imageData)) {
                    bordersFound.push(pixelCoordinates);
                }

                // If we've found one border, we're now looking for the border between color2 and color3
                else if (bordersFound.length === 2 && this.isBorderBetweenColors(color, jumpbackPixelColor, color4, color3, pixelCoordinates.x, pixelCoordinates.y, jumpbackDistance, imageData)) {
                    bordersFound.push(pixelCoordinates);
                }
            }

            if (bordersFound.length === 3) {
                const block1Dimensions = this.findBlockSize(color1, bordersFound[0].x - jumpbackDistance, bordersFound[0].y, imageData);
                const block2Dimensions = this.findBlockSize(color2, bordersFound[0].x, bordersFound[0].y, imageData, true);
                const block3Dimensions = this.findBlockSize(color3, bordersFound[1].x, bordersFound[0].y, imageData, true);
                const block4Dimensions = this.findBlockSize(color4, bordersFound[2].x, bordersFound[0].y, imageData, true);

                return {
                    centerPoint: {
                        x: bordersFound[1].x,
                        y: bordersFound[1].y + block2Dimensions.verticalSize / 2
                    },
                    blocks: [
                        {
                            x: bordersFound[0].x - block1Dimensions.horizontalSize,
                            y: bordersFound[0].y,
                            width: block1Dimensions.horizontalSize,
                            height: block1Dimensions.verticalSize,
                            color: color1
                        },
                        {
                            x: bordersFound[0].x,
                            y: bordersFound[0].y,
                            width: block2Dimensions.horizontalSize,
                            height: block2Dimensions.verticalSize,
                            color: color2
                        },
                        {
                            x: bordersFound[1].x,
                            y: bordersFound[1].y,
                            width: block3Dimensions.horizontalSize,
                            height: block3Dimensions.verticalSize,
                            color: color3
                        },
                        {
                            x: bordersFound[2].x,
                            y: bordersFound[2].y,
                            width: block4Dimensions.horizontalSize,
                            height: block4Dimensions.verticalSize,
                            color: color4
                        }
                    ]
                };
            }
        }
    }

    public static isPixelColor(color: Color, x: number, y: number, imageData: ImageData) {
        const data = imageData!.data;
        const location = this.arrayLocationFromXY(x, y, imageData!.width) * 4;
        const pixelColor = Color({
            r: data[location],
            g: data[location + 1],
            b: data[location + 2],
            alpha: data[location + 3],
        });

        return this.matchesColorWithinThreshold(color, pixelColor);
    }
}