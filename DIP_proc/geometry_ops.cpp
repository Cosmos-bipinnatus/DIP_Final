#include "pch.h"
#include <cmath>
#include "image_lib.h"

extern "C" {

__declspec(dllexport) void rotate_image(int *f, int w, int h, int d, int *g,
                                        int newW, int newH, double angle_deg,
                                        int mode, int bg_r, int bg_g, int bg_b,
                                        int bg_a) {
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
              if (d == 4)
                g[dst_idx + 3] = bg_a;
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
              if (d == 4)
                g[dst_idx + 3] = bg_a;
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
                double val = w00 * f[(y0 * w + x0) * d + c] +
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
            if (d == 4)
              g[dst_idx + 3] = bg_a;
          }
        }
      }
    }
  }
}

__declspec(dllexport) void scale_image(int *f, int w, int h, int d, int *g,
                                       int newW, int newH, int mode) {
  if (f == nullptr || g == nullptr || w <= 0 || h <= 0 || newW <= 0 || newH <= 0 || d <= 0) {
    return;
  }

  double scaleX = (double)w / newW;
  double scaleY = (double)h / newH;

  if (mode == 0) {
    // ─── Nearest Neighbor Interpolation ───
    for (int y = 0; y < newH; y++) {
      double src_y = ((double)y + 0.5) * scaleY - 0.5;
      int iy = (int)std::floor(src_y + 0.5);
      if (iy < 0) iy = 0;
      if (iy > h - 1) iy = h - 1;

      for (int x = 0; x < newW; x++) {
        double src_x = ((double)x + 0.5) * scaleX - 0.5;
        int ix = (int)std::floor(src_x + 0.5);
        if (ix < 0) ix = 0;
        if (ix > w - 1) ix = w - 1;

        int src_idx = (iy * w + ix) * d;
        int dst_idx = (y * newW + x) * d;
        for (int c = 0; c < d; c++) {
          g[dst_idx + c] = f[src_idx + c];
        }
      }
    }
  } else if (mode == 1) {
    // ─── Bilinear Interpolation ───
    for (int y = 0; y < newH; y++) {
      double src_y = ((double)y + 0.5) * scaleY - 0.5;
      int y1 = (int)std::floor(src_y);
      int y2 = y1 + 1;
      double dy = src_y - y1;

      // Clamp weights to [0.0, 1.0]
      if (dy < 0.0) dy = 0.0;
      if (dy > 1.0) dy = 1.0;

      // Clamp coordinates
      if (y1 < 0) y1 = 0;
      if (y1 > h - 1) y1 = h - 1;
      if (y2 < 0) y2 = 0;
      if (y2 > h - 1) y2 = h - 1;

      for (int x = 0; x < newW; x++) {
        double src_x = ((double)x + 0.5) * scaleX - 0.5;
        int x1 = (int)std::floor(src_x);
        int x2 = x1 + 1;
        double dx = src_x - x1;

        // Clamp weights to [0.0, 1.0]
        if (dx < 0.0) dx = 0.0;
        if (dx > 1.0) dx = 1.0;

        // Clamp coordinates
        if (x1 < 0) x1 = 0;
        if (x1 > w - 1) x1 = w - 1;
        if (x2 < 0) x2 = 0;
        if (x2 > w - 1) x2 = w - 1;

        double w00 = (1.0 - dx) * (1.0 - dy);
        double w10 = dx * (1.0 - dy);
        double w01 = (1.0 - dx) * dy;
        double w11 = dx * dy;

        int dst_idx = (y * newW + x) * d;

        for (int c = 0; c < d; c++) {
          double val = w00 * f[(y1 * w + x1) * d + c] +
                       w10 * f[(y1 * w + x2) * d + c] +
                       w01 * f[(y2 * w + x1) * d + c] +
                       w11 * f[(y2 * w + x2) * d + c];
          
          // Clamp result to [0, 255] and round
          int pixel_val = (int)(val + 0.5);
          if (pixel_val < 0) pixel_val = 0;
          if (pixel_val > 255) pixel_val = 255;

          g[dst_idx + c] = pixel_val;
        }
      }
    }
  }
}

}
