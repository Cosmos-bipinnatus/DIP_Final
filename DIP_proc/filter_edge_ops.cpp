#include "pch.h"
#include <cmath>
#include <cstdlib>
#include <cstring>
#include "image_lib.h"

extern "C" {

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
  if (f == nullptr || g == nullptr || w <= 0 || h <= 0 || d <= 0) {
    return;
  }

  int total_pixels = w * h;
  unsigned char *gray = new unsigned char[total_pixels];
  for (int i = 0; i < total_pixels; i++) {
    if (d == 1) {
      gray[i] = (unsigned char)f[i];
    } else {
      int idx = i * d;
      double b = f[idx + 0];
      double g_val = f[idx + 1];
      double r = f[idx + 2];
      int gray_val = (int)(r * 0.299 + g_val * 0.587 + b * 0.114 + 0.5);
      if (gray_val < 0) gray_val = 0;
      if (gray_val > 255) gray_val = 255;
      gray[i] = (unsigned char)gray_val;
    }
  }

  // 1. Sobel Edge Extraction
  unsigned char *edges = new unsigned char[total_pixels];
  memset(edges, 0, total_pixels);

  for (int y = 1; y < h - 1; y++) {
    for (int x = 1; x < w - 1; x++) {
      int gx = -gray[(y - 1) * w + (x - 1)] + gray[(y - 1) * w + (x + 1)]
               - 2 * gray[y * w + (x - 1)] + 2 * gray[y * w + (x + 1)]
               - gray[(y + 1) * w + (x - 1)] + gray[(y + 1) * w + (x + 1)];

      int gy = -gray[(y - 1) * w + (x - 1)] - 2 * gray[(y - 1) * w + x] - gray[(y - 1) * w + (x + 1)]
               + gray[(y + 1) * w + (x - 1)] + 2 * gray[(y + 1) * w + x] + gray[(y + 1) * w + (x + 1)];

      int mag = abs(gx) + abs(gy);
      if (mag > 100) {
        edges[y * w + x] = 255;
      }
    }
  }

  // 2. Hough Transform Voting
  int D = (int)(sqrt((double)(w * w + h * h)) + 0.5);
  int num_theta = 180;
  int num_rho = 2 * D + 1;

  int *accumulator = new int[num_theta * num_rho];
  memset(accumulator, 0, num_theta * num_rho * sizeof(int));

  const double PI = 3.14159265358979323846;
  double *cosTable = new double[num_theta];
  double *sinTable = new double[num_theta];
  for (int theta = 0; theta < num_theta; theta++) {
    double rad = theta * PI / 180.0;
    cosTable[theta] = cos(rad);
    sinTable[theta] = sin(rad);
  }

  for (int y = 0; y < h; y++) {
    for (int x = 0; x < w; x++) {
      if (edges[y * w + x] == 255) {
        for (int theta = 0; theta < num_theta; theta++) {
          double rho = x * cosTable[theta] + y * sinTable[theta];
          int rho_idx = (int)(rho + (rho >= 0 ? 0.5 : -0.5)) + D;
          if (rho_idx >= 0 && rho_idx < num_rho) {
            accumulator[theta * num_rho + rho_idx]++;
          }
        }
      }
    }
  }

  // 3. Copy input f to output g
  int total_elements = total_pixels * d;
  for (int i = 0; i < total_elements; i++) {
    g[i] = f[i];
  }

  // 4. Peak Detection and Red Lines Overlay
  for (int theta = 0; theta < num_theta; theta++) {
    for (int r = 0; r < num_rho; r++) {
      int votes = accumulator[theta * num_rho + r];
      if (votes >= houghThreshold) {
        bool is_local_max = true;
        for (int dt = -2; dt <= 2; dt++) {
          for (int dr = -2; dr <= 2; dr++) {
            if (dt == 0 && dr == 0) continue;

            int neighbor_theta = theta + dt;
            int neighbor_r = r + dr;

            if (neighbor_theta < 0) {
              neighbor_theta += num_theta;
            } else if (neighbor_theta >= num_theta) {
              neighbor_theta -= num_theta;
            }

            if (neighbor_r >= 0 && neighbor_r < num_rho) {
              if (accumulator[neighbor_theta * num_rho + neighbor_r] > votes) {
                is_local_max = false;
                break;
              }
              if (accumulator[neighbor_theta * num_rho + neighbor_r] == votes) {
                if (neighbor_theta < theta || (neighbor_theta == theta && neighbor_r < r)) {
                  is_local_max = false;
                  break;
                }
              }
            }
          }
          if (!is_local_max) break;
        }

        if (is_local_max) {
          double rho_val = r - D;
          double cos_t = cosTable[theta];
          double sin_t = sinTable[theta];

          if (abs(sin_t) > abs(cos_t)) {
            for (int x = 0; x < w; x++) {
              double y_val = (rho_val - x * cos_t) / sin_t;
              int y = (int)(y_val + (y_val >= 0 ? 0.5 : -0.5));
              if (y >= 0 && y < h) {
                if (d == 1) {
                  g[y * w + x] = 255;
                } else {
                  int idx = (y * w + x) * d;
                  g[idx + 0] = 0;   // Blue
                  g[idx + 1] = 0;   // Green
                  g[idx + 2] = 255; // Red
                }
              }
            }
          } else {
            for (int y = 0; y < h; y++) {
              double x_val = (rho_val - y * sin_t) / cos_t;
              int x = (int)(x_val + (x_val >= 0 ? 0.5 : -0.5));
              if (x >= 0 && x < w) {
                if (d == 1) {
                  g[y * w + x] = 255;
                } else {
                  int idx = (y * w + x) * d;
                  g[idx + 0] = 0;   // Blue
                  g[idx + 1] = 0;   // Green
                  g[idx + 2] = 255; // Red
                }
              }
            }
          }
        }
      }
    }
  }

  delete[] gray;
  delete[] edges;
  delete[] accumulator;
  delete[] cosTable;
  delete[] sinTable;
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
