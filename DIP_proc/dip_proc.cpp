#include "pch.h"
#include <cmath>

#define DIPPROC_UNUSED(x) UNREFERENCED_PARAMETER(x)

// Export with C linkage using __declspec(dllexport) directly to prevent Name
// Mangling
extern "C" {

__declspec(dllexport) void encode_gray(int *f, int w, int h, int d, int *g) {
  if (d == 3) { // is BGR img
    for (int y = 0; y < h; y++) {
      for (int x = 0; x < w; x++) {
        int idx = (y * w + x) * 3;
        double b_val = f[idx + 0];
        double g_val = f[idx + 1];
        double r_val = f[idx + 2];
        int gray_val = (int)(r_val * 0.299 + g_val * 0.587 + b_val * 0.114);
        g[idx + 0] = gray_val;
        g[idx + 1] = gray_val;
        g[idx + 2] = gray_val;
      }
    }
  } else if (d == 1) { // direct output
    for (int y = 0; y < h; y++) {
      for (int x = 0; x < w; x++) {
        int idx = y * w + x;
        g[idx] = f[idx];
      }
    }
  }
}

__declspec(dllexport) void bit_plane_slice(int *f, int w, int h, int d, int *g,
                                           int plane, int binarize) {
  if (!f || !g || w <= 0 || h <= 0 || plane < 0 || plane > 7) {
    return;
  }
  if (d != 1 && d != 3) {
    return;
  }

  int mask = 1 << plane;

  for (int j = 0; j < h; j++) {
    for (int i = 0; i < w; i++) {
      if (d == 1) {
        int idx = j * w + i;
        int pixel = f[idx];
        int bitVal = pixel & mask;
        g[idx] = (binarize != 0) ? (bitVal ? 255 : 0) : bitVal;
      } else if (d == 3) {
        int idx = (j * w + i) * 3;
        int b_val = f[idx + 0];
        int g_val = f[idx + 1];
        int r_val = f[idx + 2];

        int b_bit = b_val & mask;
        int g_bit = g_val & mask;
        int r_bit = r_val & mask;

        if (binarize != 0) {
          g[idx + 0] = b_bit ? 255 : 0;
          g[idx + 1] = g_bit ? 255 : 0;
          g[idx + 2] = r_bit ? 255 : 0;
        } else {
          g[idx + 0] = b_bit;
          g[idx + 1] = g_bit;
          g[idx + 2] = r_bit;
        }
      }
    }
  }
}

__declspec(dllexport) void adjust_brightness_contrast(int *f, int w, int h,
                                                      int d, int *g,
                                                      double alpha, int beta) {
  if (f == nullptr || g == nullptr || w <= 0 || h <= 0 || (d != 1 && d != 3)) {
    return;
  }

  int lut[256];
  if (alpha >= 0.0) {
    // Linear brightness & contrast adjustment
    for (int i = 0; i < 256; i++) {
      double val = alpha * (double)i + (double)beta;
      int rounded_val = (int)(val + (val >= 0.0 ? 0.5 : -0.5));
      if (rounded_val < 0) rounded_val = 0;
      if (rounded_val > 255) rounded_val = 255;
      lut[i] = rounded_val;
    }
  } else {
    // Non-linear Gamma correction
    double gamma = -alpha;
    lut[0] = 0;
    for (int i = 1; i < 256; i++) {
      double val = 255.0 * std::pow((double)i / 255.0, gamma);
      int rounded_val = (int)(val + 0.5);
      if (rounded_val < 0) rounded_val = 0;
      if (rounded_val > 255) rounded_val = 255;
      lut[i] = rounded_val;
    }
  }

  int total_elements = (d == 1) ? (w * h) : (w * h * 3);
  for (int i = 0; i < total_elements; i++) {
    int pixel_val = f[i];
    if (pixel_val < 0) pixel_val = 0;
    if (pixel_val > 255) pixel_val = 255;
    g[i] = lut[pixel_val];
  }
}

__declspec(dllexport) void calculate_histogram(int *f, int w, int h, int d,
                                               int *histB, int *histG,
                                               int *histR) {
  // Initialize histograms to 0
  if (histB) {
    for (int i = 0; i < 256; i++)
      histB[i] = 0;
  }
  if (histG) {
    for (int i = 0; i < 256; i++)
      histG[i] = 0;
  }
  if (histR) {
    for (int i = 0; i < 256; i++)
      histR[i] = 0;
  }

  if (d == 1) {
    if (histB) {
      for (int y = 0; y < h; y++) {
        for (int x = 0; x < w; x++) {
          int idx = y * w + x;
          int gray_val = f[idx];
          if (gray_val >= 0 && gray_val <= 255) {
            histB[gray_val]++;
          }
        }
      }
    }
  } else if (d == 3 || d == 4) {
    for (int y = 0; y < h; y++) {
      for (int x = 0; x < w; x++) {
        int idx = (y * w + x) * d;
        if (d == 4 && f[idx + 3] == 0) {
          continue; // Skip transparent pixels
        }
        int b_val = f[idx + 0];
        int g_val = f[idx + 1];
        int r_val = f[idx + 2];

        if (histB && b_val >= 0 && b_val <= 255)
          histB[b_val]++;
        if (histG && g_val >= 0 && g_val <= 255)
          histG[g_val]++;
        if (histR && r_val >= 0 && r_val <= 255)
          histR[r_val]++;
      }
    }
  }
}

__declspec(dllexport) void histogram_equalization(int *f, int w, int h, int d, int *g) {
  if (f == nullptr || g == nullptr || w <= 0 || h <= 0 || (d != 1 && d != 3)) {
    return;
  }

  int hist[3][256] = {0};
  int lut[3][256] = {0};
  double scale_factor = 255.0 / (double)(w * h);

  if (d == 1) {
    // 1. Calculate histogram
    for (int y = 0; y < h; y++) {
      for (int x = 0; x < w; x++) {
        int idx = y * w + x;
        int gray_val = f[idx];
        if (gray_val >= 0 && gray_val <= 255) {
          hist[0][gray_val]++;
        }
      }
    }
    // 2. Calculate CDF and LUT
    int sum = 0;
    for (int i = 0; i < 256; i++) {
      sum += hist[0][i];
      int val = (int)(sum * scale_factor + 0.5);
      if (val < 0) val = 0;
      if (val > 255) val = 255;
      lut[0][i] = val;
    }
    // 3. Map pixels
    for (int y = 0; y < h; y++) {
      for (int x = 0; x < w; x++) {
        int idx = y * w + x;
        int gray_val = f[idx];
        if (gray_val < 0) gray_val = 0;
        if (gray_val > 255) gray_val = 255;
        g[idx] = lut[0][gray_val];
      }
    }
  } else if (d == 3) {
    // 1. Calculate histogram
    for (int y = 0; y < h; y++) {
      for (int x = 0; x < w; x++) {
        int idx = (y * w + x) * 3;
        int b_val = f[idx + 0];
        int g_val = f[idx + 1];
        int r_val = f[idx + 2];

        if (b_val >= 0 && b_val <= 255) hist[0][b_val]++;
        if (g_val >= 0 && g_val <= 255) hist[1][g_val]++;
        if (r_val >= 0 && r_val <= 255) hist[2][r_val]++;
      }
    }
    // 2. Calculate CDF and LUT
    for (int i = 0; i < 3; i++) {
      int sum = 0; // Reset sum for each channel
      for (int j = 0; j < 256; j++) {
        sum += hist[i][j];
        int val = (int)(sum * scale_factor + 0.5);
        if (val < 0) val = 0;
        if (val > 255) val = 255;
        lut[i][j] = val;
      }
    }
    // 3. Map pixels
    for (int y = 0; y < h; y++) {
      for (int x = 0; x < w; x++) {
        int idx = (y * w + x) * 3;
        int b_val = f[idx + 0];
        int g_val = f[idx + 1];
        int r_val = f[idx + 2];

        if (b_val < 0) b_val = 0;
        if (b_val > 255) b_val = 255;
        if (g_val < 0) g_val = 0;
        if (g_val > 255) g_val = 255;
        if (r_val < 0) r_val = 0;
        if (r_val > 255) r_val = 255;

        g[idx + 0] = lut[0][b_val];
        g[idx + 1] = lut[1][g_val];
        g[idx + 2] = lut[2][r_val];
      }
    }
  }
}

__declspec(dllexport) void spatial_filter(int *f, int w, int h, int d, int *g,
                                          double *kernel, int kSize,
                                          double divisor, double offset) {
  DIPPROC_UNUSED(f);
  DIPPROC_UNUSED(w);
  DIPPROC_UNUSED(h);
  DIPPROC_UNUSED(d);
  DIPPROC_UNUSED(g);
  DIPPROC_UNUSED(kernel);
  DIPPROC_UNUSED(kSize);
  DIPPROC_UNUSED(divisor);
  DIPPROC_UNUSED(offset);
}

__declspec(dllexport) void rotate_image(int *f, int w, int h, int d, int *g,
                                        int newW, int newH, double angle_deg,
                                        int mode, int bg_r, int bg_g, int bg_b, int bg_a) {
  // Defensive checks
  if (f == nullptr || g == nullptr || w <= 0 || h <= 0 || newW <= 0 ||
      newH <= 0) {
    return;
  }
  if (d != 1 && d != 3 && d != 4) {
    return;
  }

  // Convert degrees to radians
  const double PI = 3.14159265358979323846;
  double theta = angle_deg * PI / 180.0;

  // Pre-compute trigonometric values (computed once, used in every pixel)
  double cos_t = std::cos(theta);
  double sin_t = std::sin(theta);

  // Center coordinates for source and destination
  double cx_src = w / 2.0;
  double cy_src = h / 2.0;
  double cx_dst = newW / 2.0;
  double cy_dst = newH / 2.0;

  if (mode == 2) {
    // ─── Forward Mapping ───
    // Initialize entire output to specified background color
    int total_pixels = newW * newH;
    if (d == 1) {
      int bg_gray = (int)(0.299 * bg_r + 0.587 * bg_g + 0.114 * bg_b);
      for (int i = 0; i < total_pixels; i++) {
        g[i] = bg_gray;
      }
    } else { // d == 3 or d == 4
      for (int i = 0; i < total_pixels; i++) {
        int dst_idx = i * d;
        g[dst_idx + 0] = bg_b;
        g[dst_idx + 1] = bg_g;
        g[dst_idx + 2] = bg_r;
        if (d == 4) {
          g[dst_idx + 3] = bg_a; // Alpha channel
        }
      }
    }

    // Iterate over each source pixel and map to destination
    for (int y = 0; y < h; y++) {
      for (int x = 0; x < w; x++) {
        double dx = (double)x - cx_src;
        double dy = (double)y - cy_src;

        // Forward rotation (image coords: positive angle = clockwise)
        // R_img(θ) = [cos(θ)  sin(θ)]
        //            [-sin(θ) cos(θ)]
        double dst_xf = dx * cos_t + dy * sin_t + cx_dst;
        double dst_yf = -dx * sin_t + dy * cos_t + cy_dst;

        // Round to nearest integer
        int ix = (int)std::floor(dst_xf + 0.5);
        int iy = (int)std::floor(dst_yf + 0.5);

        // Boundary check on destination canvas
        if (ix >= 0 && ix < newW && iy >= 0 && iy < newH) {
          int src_idx = (y * w + x) * d;
          int dst_idx = (iy * newW + ix) * d;
          for (int c = 0; c < d; c++) {
            g[dst_idx + c] = f[src_idx + c];
          }
        }
      }
    }

  } else {
    // ─── Backward Mapping (mode == 0: Nearest, mode == 1: Bilinear) ───
    // Iterate over each destination pixel and find source coordinate
    for (int yp = 0; yp < newH; yp++) {
      for (int xp = 0; xp < newW; xp++) {
        double dx = (double)xp - cx_dst;
        double dy = (double)yp - cy_dst;

        // Inverse rotation (image coords)
        // R_img^-1(θ) = [cos(θ)  -sin(θ)]
        //               [sin(θ)   cos(θ)]
        double src_x = dx * cos_t - dy * sin_t + cx_src;
        double src_y = dx * sin_t + dy * cos_t + cy_src;

        if (mode == 0) {
          // ── Nearest Neighbor ──
          int ix = (int)std::floor(src_x + 0.5);
          int iy = (int)std::floor(src_y + 0.5);

          if (ix >= 0 && ix < w && iy >= 0 && iy < h) {
            int src_idx = (iy * w + ix) * d;
            int dst_idx = (yp * newW + xp) * d;
            for (int c = 0; c < d; c++) {
              g[dst_idx + c] = f[src_idx + c];
            }
          } else {
            // Out of bounds: fill with background color
            if (d == 1) {
              int bg_gray = (int)(0.299 * bg_r + 0.587 * bg_g + 0.114 * bg_b);
              g[yp * newW + xp] = bg_gray;
            } else {
              int dst_idx = (yp * newW + xp) * d;
              g[dst_idx + 0] = bg_b;
              g[dst_idx + 1] = bg_g;
              g[dst_idx + 2] = bg_r;
              if (d == 4) g[dst_idx + 3] = bg_a;
            }
          }

        } else if (mode == 1) {
          // ── Bilinear Interpolation ──
          int x0 = (int)std::floor(src_x);
          int y0 = (int)std::floor(src_y);
          int x1 = x0 + 1;
          int y1 = y0 + 1;

          // Strict boundary: all 4 neighbors must be within bounds
          if (x0 < 0 || x1 >= w || y0 < 0 || y1 >= h) {
            if (d == 1) {
              int bg_gray = (int)(0.299 * bg_r + 0.587 * bg_g + 0.114 * bg_b);
              g[yp * newW + xp] = bg_gray;
            } else {
              int dst_idx = (yp * newW + xp) * d;
              g[dst_idx + 0] = bg_b;
              g[dst_idx + 1] = bg_g;
              g[dst_idx + 2] = bg_r;
              if (d == 4) g[dst_idx + 3] = bg_a;
            }
          } else {
            double a = src_x - (double)x0; // fractional x
            double b = src_y - (double)y0; // fractional y
            double w00 = (1.0 - a) * (1.0 - b);
            double w10 = a * (1.0 - b);
            double w01 = (1.0 - a) * b;
            double w11 = a * b;

            if (d == 1) {
              double val = w00 * f[y0 * w + x0] + w10 * f[y0 * w + x1] +
                           w01 * f[y1 * w + x0] + w11 * f[y1 * w + x1];
              int rounded = (int)(val + 0.5);
              if (rounded < 0)
                rounded = 0;
              if (rounded > 255)
                rounded = 255;
              g[yp * newW + xp] = rounded;
            } else { // d == 3 or d == 4, interpolate each channel independently
              for (int c = 0; c < d; c++) {
                double val =
                    w00 * f[(y0 * w + x0) * d + c] +
                    w10 * f[(y0 * w + x1) * d + c] +
                    w01 * f[(y1 * w + x0) * d + c] +
                    w11 * f[(y1 * w + x1) * d + c];
                int rounded = (int)(val + 0.5);
                if (rounded < 0)
                  rounded = 0;
                if (rounded > 255)
                  rounded = 255;
                g[(yp * newW + xp) * d + c] = rounded;
              }
            }
          }

        } else {
          // Unknown mode: fill with background color
          if (d == 1) {
            int bg_gray = (int)(0.299 * bg_r + 0.587 * bg_g + 0.114 * bg_b);
            g[yp * newW + xp] = bg_gray;
          } else {
            int dst_idx = (yp * newW + xp) * d;
            g[dst_idx + 0] = bg_b;
            g[dst_idx + 1] = bg_g;
            g[dst_idx + 2] = bg_r;
            if (d == 4) g[dst_idx + 3] = bg_a;
          }
        }
      }
    }
  }
}

__declspec(dllexport) void scale_image(int *f, int w, int h, int d, int *g,
                                       int newW, int newH, int mode) {
  DIPPROC_UNUSED(f);
  DIPPROC_UNUSED(w);
  DIPPROC_UNUSED(h);
  DIPPROC_UNUSED(d);
  DIPPROC_UNUSED(g);
  DIPPROC_UNUSED(newW);
  DIPPROC_UNUSED(newH);
  DIPPROC_UNUSED(mode);
}

__declspec(dllexport) void manual_threshold(int *f, int w, int h, int d, int *g,
                                            int T) {
  DIPPROC_UNUSED(f);
  DIPPROC_UNUSED(w);
  DIPPROC_UNUSED(h);
  DIPPROC_UNUSED(d);
  DIPPROC_UNUSED(g);
  DIPPROC_UNUSED(T);
}

__declspec(dllexport) void otsu_threshold(int *f, int w, int h, int d, int *g) {
  DIPPROC_UNUSED(f);
  DIPPROC_UNUSED(w);
  DIPPROC_UNUSED(h);
  DIPPROC_UNUSED(d);
  DIPPROC_UNUSED(g);
}

__declspec(dllexport) void detect_sobel(int *f, int w, int h, int d, int *g) {
  DIPPROC_UNUSED(f);
  DIPPROC_UNUSED(w);
  DIPPROC_UNUSED(h);
  DIPPROC_UNUSED(d);
  DIPPROC_UNUSED(g);
}

__declspec(dllexport) void detect_canny(int *f, int w, int h, int d, int *g,
                                        double lowThresh, double highThresh) {
  DIPPROC_UNUSED(f);
  DIPPROC_UNUSED(w);
  DIPPROC_UNUSED(h);
  DIPPROC_UNUSED(d);
  DIPPROC_UNUSED(g);
  DIPPROC_UNUSED(lowThresh);
  DIPPROC_UNUSED(highThresh);
}

__declspec(dllexport) void detect_lines_hough(int *f, int w, int h, int d,
                                              int *g, int houghThreshold) {
  DIPPROC_UNUSED(f);
  DIPPROC_UNUSED(w);
  DIPPROC_UNUSED(h);
  DIPPROC_UNUSED(d);
  DIPPROC_UNUSED(g);
  DIPPROC_UNUSED(houghThreshold);
}

__declspec(dllexport) void detect_circles_hough(int *f, int w, int h, int d,
                                                int *g, int rMin, int rMax,
                                                int houghThreshold) {
  DIPPROC_UNUSED(f);
  DIPPROC_UNUSED(w);
  DIPPROC_UNUSED(h);
  DIPPROC_UNUSED(d);
  DIPPROC_UNUSED(g);
  DIPPROC_UNUSED(rMin);
  DIPPROC_UNUSED(rMax);
  DIPPROC_UNUSED(houghThreshold);
}
}
