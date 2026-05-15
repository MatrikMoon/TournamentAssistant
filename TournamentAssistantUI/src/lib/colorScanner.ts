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
 *
 * Broadcast tolerance:
 * Colour matching is done in HSL space with asymmetric tolerances.
 * Broadcast pipelines (OBS, Twitch transcode, capture cards) tend to:
 *  - Preserve hue reasonably well -> tight hue tolerance (+-18 deg)
 *  - Desaturate moderately -> medium saturation tolerance (+-30 pts)
 *  - Shift lightness via gamma/levels -> loose lightness tolerance (+-35 pts)
 *
 * Hue wrapping is handled so that e.g. red at 358° vs 2° matches correctly.
 */

const jumpbackDistance = 15;
const minimumBlockSize = 20;

// HSL tolerances tuned around typical streaming / capture pipelines
const HUE_TOLERANCE = 18; // degrees (hue is usually preserved best)
const SATURATION_TOLERANCE = 30; // percentage points (compression desaturates)
const LIGHTNESS_TOLERANCE = 35; // percentage points (gamma / levels shift this most)

export type Point = { x: number; y: number };
export type Block = {
	x: number;
	y: number;
	width: number;
	height: number;
	color: Color;
};
export type ColorBar = { centerPoint: Point; blocks: Block[] };

export class ColorScanner {
	// Converting array location to X and Y
	public static xyFromArrayLocation(
		arrayLocation: number,
		imageWidth: number,
	): Point {
		return {
			x: arrayLocation % imageWidth,
			y: (arrayLocation / imageWidth) | 0,
		};
	}

	// Converting X and Y to array location
	// NOTE: This DOES NOT take into account the fact that
	// a "pixel" is 4 numbers. You'll need to multiply by 4 if
	// you need the index for something like that
	public static arrayLocationFromXY(
		x: number,
		y: number,
		imageWidth: number,
	): number {
		return y * imageWidth + x;
	}

	private static hueDiff(h1: number, h2: number): number {
		// Shortest angular distance on a 360° circle
		const diff = Math.abs(h1 - h2) % 360;
		return diff > 180 ? 360 - diff : diff;
	}

	// Returns true if the two provided colors are close enough to match
	// Stream encoders can tweak colors quite a bit, especially saturation
	// and brightness, so we compare in HSL space with separate tolerances
	private static matchesColorWithinThreshold(
		color1?: Color,
		color2?: Color,
	): boolean {
		if (!color1 || !color2) return false;

		const hueDiff = this.hueDiff(color1.hue(), color2.hue());
		const satDiff = Math.abs(color1.saturationl() - color2.saturationl());
		const litDiff = Math.abs(color1.lightness() - color2.lightness());

		// Near-grey colors don't have a stable hue value,
		// so ignore hue entirely in that case
		const isGreyish =
			color1.saturationl() < 10 || color2.saturationl() < 10;

		if (!isGreyish && hueDiff > HUE_TOLERANCE) return false;
		if (satDiff > SATURATION_TOLERANCE) return false;
		if (litDiff > LIGHTNESS_TOLERANCE) return false;

		return true;
	}

	// When we find a border, we'll want to look backwards and down
	// to make sure the color block is actually large enough to be valid.
	// Searches backwards horizontally by default unless `forward` is true
	private static findBlockSize(
		blockColor: Color,
		x: number,
		y: number,
		imageData: ImageData,
		forward: boolean = false,
	): { horizontalSize: number; verticalSize: number } {
		let horizontalSize = 0;
		let verticalSize = 0;

		const data = imageData.data;

		// Horizontal check //
		const startingPixel = this.arrayLocationFromXY(
			x,
			y,
			imageData.width,
		);

		// Either scan to the start or end of the current row
		const rowEnd = forward
			? (y + 1) * imageData.width
			: y * imageData.width;

		for (
			let pixel = startingPixel;
			forward ? pixel < rowEnd : pixel > rowEnd;
			forward ? pixel++ : pixel--
		) {
			const loc = pixel * 4;

			const currentPixelColor = Color({
				r: data[loc],
				g: data[loc + 1],
				b: data[loc + 2],
			});

			// Keep counting until the color changes
			if (
				this.matchesColorWithinThreshold(
					currentPixelColor,
					blockColor,
				)
			) {
				horizontalSize++;
			} else {
				break;
			}
		}

		// Vertical scans always go downward
		const colEnd =
			this.arrayLocationFromXY(
				x,
				imageData.height,
				imageData.width,
			) + 1;

		for (
			let pixel = startingPixel;
			pixel < colEnd;
			pixel += imageData.width
		) {
			const loc = pixel * 4;

			const currentPixelColor = Color({
				r: data[loc],
				g: data[loc + 1],
				b: data[loc + 2],
			});

			// Keep counting until the color changes
			if (
				this.matchesColorWithinThreshold(
					currentPixelColor,
					blockColor,
				)
			) {
				verticalSize++;
			} else {
				break;
			}
		}

		return { horizontalSize, verticalSize };
	}

	// Takes the current pixel and the jumpback pixel to determine
	// whether we've found a border between two color blocks
	private static isBorderBetweenColors(
		currentPixelColor: Color,
		jumpbackPixelColor: Color,
		currentLookingForColor: Color,
		jumpbackLookingForColor: Color,
		x: number,
		y: number,
		imageData: ImageData,
	): boolean {
		if (
			!this.matchesColorWithinThreshold(
				currentPixelColor,
				currentLookingForColor,
			) ||
			!this.matchesColorWithinThreshold(
				jumpbackPixelColor,
				jumpbackLookingForColor,
			)
		) {
			return false;
		}

		const currentBlockSize = this.findBlockSize(
			currentLookingForColor,
			x,
			y,
			imageData,
			true,
		);

		const lastBlockSize = this.findBlockSize(
			jumpbackLookingForColor,
			x - jumpbackDistance,
			y,
			imageData,
		);

		return (
			currentBlockSize.horizontalSize >= minimumBlockSize &&
			currentBlockSize.verticalSize >= minimumBlockSize &&
			lastBlockSize.horizontalSize >= minimumBlockSize &&
			lastBlockSize.verticalSize >= minimumBlockSize
		);
	}

	// Returns the center location of the provided sequence of colors
	// Assumes the image contains four horizontal colored blocks in order.
	// Once all three borders are found, returns the center point and
	// dimensions of each detected block
	public static getLocationOfSequence(
		color1: Color,
		color2: Color,
		color3: Color,
		color4: Color,
		imageData: ImageData,
	): ColorBar | undefined {
		let jumpbackPixelColor: Color | undefined;
		let bordersFound: Point[] = [];

		const data = imageData.data;

		for (
			let pixel = 0;
			pixel < imageData.width * imageData.height;
			pixel++
		) {
			const loc = pixel * 4;
			const coord = this.xyFromArrayLocation(
				pixel,
				imageData.width,
			);

			const color = Color({
				r: data[loc],
				g: data[loc + 1],
				b: data[loc + 2],
			});

			if (coord.x >= jumpbackDistance) {
				const jbLoc =
					this.arrayLocationFromXY(
						coord.x - jumpbackDistance,
						coord.y,
						imageData.width,
					) * 4;

				jumpbackPixelColor = Color({
					r: data[jbLoc],
					g: data[jbLoc + 1],
					b: data[jbLoc + 2],
				});
			} else {
				// Reset state at the start of each row so borders
				// don't accidentally carry over between scanlines
				jumpbackPixelColor = undefined;
				bordersFound = [];
			}

			if (!jumpbackPixelColor) continue;

			if (
				bordersFound.length === 0 &&
				this.isBorderBetweenColors(
					color,
					jumpbackPixelColor,
					color2,
					color1,
					coord.x,
					coord.y,
					imageData,
				)
			) {
				bordersFound.push(coord);
			} else if (
				bordersFound.length === 1 &&
				this.isBorderBetweenColors(
					color,
					jumpbackPixelColor,
					color3,
					color2,
					coord.x,
					coord.y,
					imageData,
				)
			) {
				bordersFound.push(coord);
			} else if (
				bordersFound.length === 2 &&
				this.isBorderBetweenColors(
					color,
					jumpbackPixelColor,
					color4,
					color3,
					coord.x,
					coord.y,
					imageData,
				)
			) {
				bordersFound.push(coord);
			}

			if (bordersFound.length === 3) {
				const block1Dim = this.findBlockSize(
					color1,
					bordersFound[0].x - jumpbackDistance,
					bordersFound[0].y,
					imageData,
				);

				const block2Dim = this.findBlockSize(
					color2,
					bordersFound[0].x,
					bordersFound[0].y,
					imageData,
					true,
				);

				const block3Dim = this.findBlockSize(
					color3,
					bordersFound[1].x,
					bordersFound[0].y,
					imageData,
					true,
				);

				const block4Dim = this.findBlockSize(
					color4,
					bordersFound[2].x,
					bordersFound[0].y,
					imageData,
					true,
				);

				return {
					centerPoint: {
						x: bordersFound[1].x,
						y:
							bordersFound[1].y +
							Math.round(block2Dim.verticalSize / 2),
					},
					blocks: [
						{
							x: bordersFound[0].x - block1Dim.horizontalSize,
							y: bordersFound[0].y,
							width: block1Dim.horizontalSize,
							height: block1Dim.verticalSize,
							color: color1,
						},
						{
							x: bordersFound[0].x,
							y: bordersFound[0].y,
							width: block2Dim.horizontalSize,
							height: block2Dim.verticalSize,
							color: color2,
						},
						{
							x: bordersFound[1].x,
							y: bordersFound[1].y,
							width: block3Dim.horizontalSize,
							height: block3Dim.verticalSize,
							color: color3,
						},
						{
							x: bordersFound[2].x,
							y: bordersFound[2].y,
							width: block4Dim.horizontalSize,
							height: block4Dim.verticalSize,
							color: color4,
						},
					],
				};
			}
		}

		return undefined;
	}

	// Checks whether the pixel at the given coordinates
	// matches the provided color within tolerance
	public static isPixelColor(
		color: Color,
		x: number,
		y: number,
		imageData: ImageData,
	): boolean {
		const data = imageData.data;

		const location =
			this.arrayLocationFromXY(x, y, imageData.width) * 4;

		const pixelColor = Color({
			r: data[location],
			g: data[location + 1],
			b: data[location + 2],
		});

		return this.matchesColorWithinThreshold(color, pixelColor);
	}

	// Extracts a square region centered around the provided coordinates
	public static extractBox(
		centerX: number,
		centerY: number,
		boxSize: number,
		imageData: ImageData,
	): ImageData {
		const halfBoxSize = Math.floor(boxSize / 2);

		const startX = Math.max(0, centerX - halfBoxSize);
		const startY = Math.max(0, centerY - halfBoxSize);

		const endX = Math.min(
			imageData.width,
			centerX + halfBoxSize,
		);

		const endY = Math.min(
			imageData.height,
			centerY + halfBoxSize,
		);

		const width = endX - startX;
		const height = endY - startY;

		const extractedData = new ImageData(width, height);

		const srcData = imageData.data;
		const dstData = extractedData.data;

		for (let y = startY; y < endY; y++) {
			for (let x = startX; x < endX; x++) {
				const srcPos = (y * imageData.width + x) * 4;

				const destPos =
					((y - startY) * width + (x - startX)) * 4;

				dstData[destPos] = srcData[srcPos];
				dstData[destPos + 1] = srcData[srcPos + 1];
				dstData[destPos + 2] = srcData[srcPos + 2];
				dstData[destPos + 3] = srcData[srcPos + 3];
			}
		}

		return extractedData;
	}
}
