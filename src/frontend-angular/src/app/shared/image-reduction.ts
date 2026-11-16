/**
 * Photos are reduced in the browser before upload: a 12 Mpx phone photo of several MB
 * becomes a JPEG of a few hundred KB, enough to read a receipt or show a food. It saves
 * upload time, the durable volumes, and keeps receipt photos well within what the
 * assistant can read. The orientation is applied and metadata (GPS included) is dropped.
 */
export interface ImageReduction {
  /** Longest side, in pixels, after reduction. */
  maxSide: number
  /** JPEG quality, 0 to 1. */
  quality: number
}

/** Receipts keep enough detail to read small print, on screen and by the assistant. */
export const RECEIPT_PHOTO: ImageReduction = { maxSide: 2560, quality: 0.85 }
/** Food and recipe pictures are only shown as cards and headers. */
export const LIBRARY_IMAGE: ImageReduction = { maxSide: 1600, quality: 0.82 }

/** Under this weight, a photo already small enough in pixels is sent untouched. */
const SMALL_FILE_BYTES = 500 * 1024

/** The reduced size, keeping the proportions; null when the image is already small enough. */
export function reducedSize(width: number, height: number, maxSide: number): { width: number; height: number } | null {
  const longest = Math.max(width, height)
  if (longest <= maxSide || longest <= 0) return null
  const ratio = maxSide / longest
  return { width: Math.max(1, Math.round(width * ratio)), height: Math.max(1, Math.round(height * ratio)) }
}

/** The JPEG name of a reduced file: `ticket.heic` → `ticket.jpg`. */
export function jpegName(name: string): string {
  const base = name.replace(/\.[^./\\]+$/, '')
  return `${base || 'photo'}.jpg`
}

/**
 * Re-encodes a photo as a smaller JPEG. Returns the original file when it is already small,
 * when the browser cannot decode it (the API then gives its usual error), or when the
 * reduction would not make it lighter.
 */
export async function reduceImage(file: File, reduction: ImageReduction): Promise<File> {
  let bitmap: ImageBitmap
  try {
    bitmap = await createImageBitmap(file, { imageOrientation: 'from-image' })
  } catch {
    return file
  }
  try {
    const size = reducedSize(bitmap.width, bitmap.height, reduction.maxSide)
    const isJpegOrPng = file.type === 'image/jpeg' || file.type === 'image/png' || file.type === 'image/webp'
    if (!size && isJpegOrPng && file.size <= SMALL_FILE_BYTES) return file
    const { width, height } = size ?? { width: bitmap.width, height: bitmap.height }
    const canvas = document.createElement('canvas')
    canvas.width = width
    canvas.height = height
    const context = canvas.getContext('2d')
    if (!context) return file
    // Transparent PNGs get a white background instead of black in JPEG.
    context.fillStyle = '#fff'
    context.fillRect(0, 0, width, height)
    context.imageSmoothingQuality = 'high'
    context.drawImage(bitmap, 0, 0, width, height)
    const blob = await new Promise<Blob | null>(resolve => canvas.toBlob(resolve, 'image/jpeg', reduction.quality))
    if (!blob || (blob.size >= file.size && isJpegOrPng)) return file
    return new File([blob], jpegName(file.name), { type: 'image/jpeg', lastModified: file.lastModified })
  } finally {
    bitmap.close()
  }
}
