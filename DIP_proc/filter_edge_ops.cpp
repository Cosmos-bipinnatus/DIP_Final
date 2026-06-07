#include "pch.h"
#include "image_lib.h"
#include <cmath>
#include <cstdlib>
#include <cstring>
#include <vector>
#include <algorithm>

// Hough Circle Help Structures and Functions
struct DetectedCircle {
  int a;
  int b;
  int r;
  int votes;
};

inline bool compareCircles(const DetectedCircle &c1, const DetectedCircle &c2) {
  return c1.votes > c2.votes;
}

inline void draw_color_pixel(int *g, int w, int h, int d, int x, int y, int r, int g_val, int b) {
  if (x >= 0 && x < w && y >= 0 && y < h) {
    if (d == 1) {
      g[y * w + x] = (r * 299 + g_val * 587 + b * 114) / 1000;
    } else {
      int idx = (y * w + x) * d;
      g[idx + 0] = b;
      g[idx + 1] = g_val;
      g[idx + 2] = r;
    }
  }
}

inline void draw_circle(int *g, int w, int h, int d, int xc, int yc, int r, int lineR, int lineG, int lineB) {
  int x = 0;
  int y = r;
  int p = 1 - r;

  auto draw_symmetric_pixels = [&](int x_offset, int y_offset) {
    draw_color_pixel(g, w, h, d, xc + x_offset, yc + y_offset, lineR, lineG, lineB);
    draw_color_pixel(g, w, h, d, xc - x_offset, yc + y_offset, lineR, lineG, lineB);
    draw_color_pixel(g, w, h, d, xc + x_offset, yc - y_offset, lineR, lineG, lineB);
    draw_color_pixel(g, w, h, d, xc - x_offset, yc - y_offset, lineR, lineG, lineB);
    draw_color_pixel(g, w, h, d, xc + y_offset, yc + x_offset, lineR, lineG, lineB);
    draw_color_pixel(g, w, h, d, xc - y_offset, yc + x_offset, lineR, lineG, lineB);
    draw_color_pixel(g, w, h, d, xc + y_offset, yc - x_offset, lineR, lineG, lineB);
    draw_color_pixel(g, w, h, d, xc - y_offset, yc - x_offset, lineR, lineG, lineB);
  };

  draw_symmetric_pixels(x, y);

  while (x < y) {
    x++;
    if (p < 0) {
      p += 2 * x + 1;
    } else {
      y--;
      p += 2 * (x - y) + 1;
    }
    draw_symmetric_pixels(x, y);
  }
}

extern "C" {

__declspec(dllexport) void convolution_filter(int *f, int w, int h, int d, int *g,
                                              double *kernel, int kSize,
                                              double divisor, double offset) {
  if (f == nullptr || g == nullptr || kernel == nullptr || w <= 0 || h <= 0 || d <= 0 || kSize <= 0) {
    return;
  }

  int kHalf = kSize / 2;
  double div = (divisor == 0.0) ? 1.0 : divisor;

  for (int y = 0; y < h; y++) {
    for (int x = 0; x < w; x++) {
      int idx = (y * w + x) * d;

      if (d == 4) {
        g[idx + 3] = f[idx + 3];
      }

      int channels_to_filter = (d >= 3) ? 3 : d;
      for (int c = 0; c < channels_to_filter; c++) {
        double sum = 0.0;
        for (int ki = -kHalf; ki <= kHalf; ki++) {
          int ny = y + ki;
          for (int kj = -kHalf; kj <= kHalf; kj++) {
            int nx = x + kj;

            double pixel_val = 0.0;
            if (nx >= 0 && nx < w && ny >= 0 && ny < h) {
              pixel_val = f[(ny * w + nx) * d + c];
            }

            sum += pixel_val * kernel[(ki + kHalf) * kSize + (kj + kHalf)];
          }
        }

        double val = sum / div + offset;
        if (val < 0.0) val = 0.0;
        if (val > 255.0) val = 255.0;

        g[idx + c] = (int)(val + 0.5);
      }
    }
  }
}


__declspec(dllexport) void detect_sobel(int *f, int w, int h, int d, int *g) {
  if (f == nullptr || g == nullptr || w <= 0 || h <= 0 || d <= 0) {
    return;
  }

  int total_pixels = w * h;
  int *temp_gray = new int[total_pixels * d];

  // Convert input to grayscale temp_gray first
  for (int i = 0; i < total_pixels; i++) {
    int idx = i * d;
    if (d == 1) {
      int v = f[i];
      if (v < 0) v = 0;
      if (v > 255) v = 255;
      temp_gray[i] = v;
    } else {
      double b = f[idx + 0];
      double gval = f[idx + 1];
      double r = f[idx + 2];
      int gray_val = (int)(r * 0.299 + gval * 0.587 + b * 0.114 + 0.5);
      if (gray_val < 0) gray_val = 0;
      if (gray_val > 255) gray_val = 255;
      temp_gray[idx + 0] = gray_val;
      temp_gray[idx + 1] = gray_val;
      temp_gray[idx + 2] = gray_val;
      if (d == 4) {
        temp_gray[idx + 3] = f[idx + 3];
      }
    }
  }

  int *tempGx = new int[total_pixels * d];
  int *tempGy = new int[total_pixels * d];

  double kernelX[9] = { -1, 0, 1, -2, 0, 2, -1, 0, 1 };
  double kernelY[9] = { -1, -2, -1, 0, 0, 0, 1, 2, 1 };

  // Call convolution_filter (reusing the custom filter core)
  convolution_filter(temp_gray, w, h, d, tempGx, kernelX, 3, 1.0, 0.0);
  convolution_filter(temp_gray, w, h, d, tempGy, kernelY, 3, 1.0, 0.0);

  // Combine X and Y gradients
  for (int i = 0; i < total_pixels; i++) {
    int idx = i * d;
    if (d == 1) {
      int gx = tempGx[i];
      int gy = tempGy[i];
      int mag = abs(gx) + abs(gy);
      if (mag > 255) mag = 255;
      if (mag < 0) mag = 0;
      g[i] = mag;
    } else {
      int gx = tempGx[idx + 0];
      int gy = tempGy[idx + 0];
      int mag = abs(gx) + abs(gy);
      if (mag > 255) mag = 255;
      if (mag < 0) mag = 0;
      g[idx + 0] = mag;
      g[idx + 1] = mag;
      g[idx + 2] = mag;
      if (d == 4) {
        g[idx + 3] = f[idx + 3]; // Preserve alpha
      }
    }
  }

  // Handle borders (set to 0)
  for (int x = 0; x < w; x++) {
    int top = 0 * w + x;
    int bottom = (h - 1) * w + x;
    if (d == 1) {
      g[top] = 0;
      g[bottom] = 0;
    } else {
      int tIdx = top * d;
      int bIdx = bottom * d;
      g[tIdx + 0] = g[tIdx + 1] = g[tIdx + 2] = 0;
      g[bIdx + 0] = g[bIdx + 1] = g[bIdx + 2] = 0;
    }
  }
  for (int y = 0; y < h; y++) {
    int left = y * w + 0;
    int right = y * w + (w - 1);
    if (d == 1) {
      g[left] = 0;
      g[right] = 0;
    } else {
      int lIdx = left * d;
      int rIdx = right * d;
      g[lIdx + 0] = g[lIdx + 1] = g[lIdx + 2] = 0;
      g[rIdx + 0] = g[rIdx + 1] = g[rIdx + 2] = 0;
    }
  }

  delete[] temp_gray;
  delete[] tempGx;
  delete[] tempGy;
}


__declspec(dllexport) void detect_canny(int *f, int w, int h, int d, int *g,
                                        double low_threshold,
                                        double high_threshold) {
  if (f == nullptr || g == nullptr || w <= 0 || h <= 0 || d <= 0) {
    return;
  }

  int total_pixels = w * h;
  int *temp_gray = new int[total_pixels * d];

  // 1. Convert to Grayscale
  for (int i = 0; i < total_pixels; i++) {
    int idx = i * d;
    if (d == 1) {
      int v = f[i];
      if (v < 0) v = 0;
      if (v > 255) v = 255;
      temp_gray[i] = v;
    } else {
      double b = f[idx + 0];
      double gval = f[idx + 1];
      double r = f[idx + 2];
      int gray_val = (int)(r * 0.299 + gval * 0.587 + b * 0.114 + 0.5);
      if (gray_val < 0) gray_val = 0;
      if (gray_val > 255) gray_val = 255;
      temp_gray[idx + 0] = gray_val;
      temp_gray[idx + 1] = gray_val;
      temp_gray[idx + 2] = gray_val;
      if (d == 4) {
        temp_gray[idx + 3] = f[idx + 3];
      }
    }
  }

  // Allocate Canny internal buffers
  double *grad_mag = new double[total_pixels];
  double *grad_dir = new double[total_pixels];
  unsigned char *nms = new unsigned char[total_pixels];

  // 2. Gaussian Blur 3x3 using convolution_filter
  int *temp_blurred = new int[total_pixels * d];
  double gaussKernel[9] = { 1, 2, 1, 2, 4, 2, 1, 2, 1 };
  convolution_filter(temp_gray, w, h, d, temp_blurred, gaussKernel, 3, 16.0, 0.0);

  // 3. Sobel Gradients using convolution_filter
  int *tempGx = new int[total_pixels * d];
  int *tempGy = new int[total_pixels * d];
  double kernelX[9] = { -1, 0, 1, -2, 0, 2, -1, 0, 1 };
  double kernelY[9] = { -1, -2, -1, 0, 0, 0, 1, 2, 1 };

  convolution_filter(temp_blurred, w, h, d, tempGx, kernelX, 3, 1.0, 0.0);
  convolution_filter(temp_blurred, w, h, d, tempGy, kernelY, 3, 1.0, 0.0);

  // Compute magnitude and direction
  for (int i = 0; i < total_pixels; i++) {
    int idx = i * d;
    double gx = tempGx[idx];
    double gy = tempGy[idx];
    grad_mag[i] = std::sqrt(gx * gx + gy * gy);
    grad_dir[i] = std::atan2(gy, gx);
    nms[i] = 0;
  }

  // 4. Non-Maximum Suppression (NMS)
  for (int y = 1; y < h - 1; y++) {
    for (int x = 1; x < w - 1; x++) {
      int idx = y * w + x;
      double mag = grad_mag[idx];
      if (mag == 0)
        continue;

      double angle = grad_dir[idx] * 180.0 / 3.141592653589793;
      if (angle < 0)
        angle += 180.0;

      double mag1 = 0, mag2 = 0;

      if ((angle >= 0 && angle < 22.5) || (angle >= 157.5 && angle <= 180)) {
        mag1 = grad_mag[y * w + (x - 1)];
        mag2 = grad_mag[y * w + (x + 1)];
      } else if (angle >= 22.5 && angle < 67.5) {
        mag1 = grad_mag[(y - 1) * w + (x + 1)];
        mag2 = grad_mag[(y + 1) * w + (x - 1)];
      } else if (angle >= 67.5 && angle < 112.5) {
        mag1 = grad_mag[(y - 1) * w + x];
        mag2 = grad_mag[(y + 1) * w + x];
      } else if (angle >= 112.5 && angle < 157.5) {
        mag1 = grad_mag[(y - 1) * w + (x - 1)];
        mag2 = grad_mag[(y + 1) * w + (x + 1)];
      }

      if (mag >= mag1 && mag >= mag2) {
        nms[idx] = (mag > 255) ? 255 : (unsigned char)mag;
      } else {
        nms[idx] = 0;
      }
    }
  }

  // 5. Double Threshold & Hysteresis
  unsigned char *edge_status = new unsigned char[total_pixels];
  int *stack = new int[total_pixels];
  int stack_top = 0;

  for (int i = 0; i < total_pixels; i++) {
    if (nms[i] >= high_threshold) {
      edge_status[i] = 2;
      stack[stack_top++] = i;
    } else if (nms[i] >= low_threshold) {
      edge_status[i] = 1;
    } else {
      edge_status[i] = 0;
    }
  }

  int dx[8] = {-1, 0, 1, -1, 1, -1, 0, 1};
  int dy[8] = {-1, -1, -1, 0, 0, 1, 1, 1};

  while (stack_top > 0) {
    int curr_idx = stack[--stack_top];
    int curr_x = curr_idx % w;
    int curr_y = curr_idx / w;

    for (int i = 0; i < 8; i++) {
      int nx = curr_x + dx[i];
      int ny = curr_y + dy[i];

      if (nx >= 0 && nx < w && ny >= 0 && ny < h) {
        int neighbor_idx = ny * w + nx;
        if (edge_status[neighbor_idx] == 1) {
          edge_status[neighbor_idx] = 2;
          stack[stack_top++] = neighbor_idx;
        }
      }
    }
  }

  // 6. Write final edges to g
  for (int y = 0; y < h; y++) {
    for (int x = 0; x < w; x++) {
      int idx = y * w + x;
      int final_val = 0;
      if (y > 0 && y < h - 1 && x > 0 && x < w - 1) {
        if (edge_status[idx] == 2) {
          final_val = 255;
        }
      }

      if (d == 1) {
        g[idx] = final_val;
      } else {
        int outIdx = idx * d;
        g[outIdx + 0] = final_val;
        g[outIdx + 1] = final_val;
        g[outIdx + 2] = final_val;
        if (d == 4)
          g[outIdx + 3] = f[outIdx + 3];
      }
    }
  }

  // 7. Cleanup
  delete[] temp_gray;
  delete[] temp_blurred;
  delete[] tempGx;
  delete[] tempGy;
  delete[] grad_mag;
  delete[] grad_dir;
  delete[] nms;
  delete[] edge_status;
  delete[] stack;
}

__declspec(dllexport) void detect_lines_hough(int *f, int w, int h, int d,
                                              int *g, int houghThreshold,
                                              int lineR, int lineG, int lineB) {
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
      if (gray_val < 0)
        gray_val = 0;
      if (gray_val > 255)
        gray_val = 255;
      gray[i] = (unsigned char)gray_val;
    }
  }

  // 1. Sobel Edge Extraction
  unsigned char *edges = new unsigned char[total_pixels];
  memset(edges, 0, total_pixels);

  for (int y = 1; y < h - 1; y++) {
    for (int x = 1; x < w - 1; x++) {
      int gx = -gray[(y - 1) * w + (x - 1)] + gray[(y - 1) * w + (x + 1)] -
               2 * gray[y * w + (x - 1)] + 2 * gray[y * w + (x + 1)] -
               gray[(y + 1) * w + (x - 1)] + gray[(y + 1) * w + (x + 1)];

      int gy = -gray[(y - 1) * w + (x - 1)] - 2 * gray[(y - 1) * w + x] -
               gray[(y - 1) * w + (x + 1)] + gray[(y + 1) * w + (x - 1)] +
               2 * gray[(y + 1) * w + x] + gray[(y + 1) * w + (x + 1)];

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

  // 4. Peak Detection and Overlay with Custom Line Color
  for (int theta = 0; theta < num_theta; theta++) {
    for (int r = 0; r < num_rho; r++) {
      int votes = accumulator[theta * num_rho + r];
      if (votes >= houghThreshold) {
        bool is_local_max = true;
        for (int dt = -2; dt <= 2; dt++) {
          for (int dr = -2; dr <= 2; dr++) {
            if (dt == 0 && dr == 0)
              continue;

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
                if (neighbor_theta < theta ||
                    (neighbor_theta == theta && neighbor_r < r)) {
                  is_local_max = false;
                  break;
                }
              }
            }
          }
          if (!is_local_max)
            break;
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
                draw_color_pixel(g, w, h, d, x, y, lineR, lineG, lineB);
              }
            }
          } else {
            for (int y = 0; y < h; y++) {
              double x_val = (rho_val - y * sin_t) / cos_t;
              int x = (int)(x_val + (x_val >= 0 ? 0.5 : -0.5));
              if (x >= 0 && x < w) {
                draw_color_pixel(g, w, h, d, x, y, lineR, lineG, lineB);
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
                                                int houghThreshold,
                                                int lineR, int lineG, int lineB) {
  if (f == nullptr || g == nullptr || w <= 0 || h <= 0 || d <= 0 || rMin < 0 || rMax < rMin) {
    return;
  }

  int total_pixels = w * h;
  unsigned char *gray = new unsigned char[total_pixels];

  // 1. Convert to grayscale
  for (int i = 0; i < total_pixels; i++) {
    if (d == 1) {
      int v = f[i];
      if (v < 0) v = 0;
      if (v > 255) v = 255;
      gray[i] = (unsigned char)v;
    } else {
      int idx = i * d;
      double b = f[idx + 0];
      double gval = f[idx + 1];
      double r = f[idx + 2];
      int gray_val = (int)(r * 0.299 + gval * 0.587 + b * 0.114 + 0.5);
      if (gray_val < 0) gray_val = 0;
      if (gray_val > 255) gray_val = 255;
      gray[i] = (unsigned char)gray_val;
    }
  }

  // 2. Sobel Edge & Gradient Computation
  unsigned char *edges = new unsigned char[total_pixels];
  memset(edges, 0, total_pixels);
  int *dx_grad = new int[total_pixels];
  int *dy_grad = new int[total_pixels];
  memset(dx_grad, 0, total_pixels * sizeof(int));
  memset(dy_grad, 0, total_pixels * sizeof(int));

  for (int y = 1; y < h - 1; y++) {
    for (int x = 1; x < w - 1; x++) {
      int idx = y * w + x;
      int gx = -gray[(y - 1) * w + (x - 1)] + gray[(y - 1) * w + (x + 1)] -
               2 * gray[y * w + (x - 1)] + 2 * gray[y * w + (x + 1)] -
               gray[(y + 1) * w + (x - 1)] + gray[(y + 1) * w + (x + 1)];

      int gy = -gray[(y - 1) * w + (x - 1)] - 2 * gray[(y - 1) * w + x] -
               gray[(y - 1) * w + (x + 1)] + gray[(y + 1) * w + (x - 1)] +
               2 * gray[(y + 1) * w + x] + gray[(y + 1) * w + (x + 1)];

      dx_grad[idx] = gx;
      dy_grad[idx] = gy;
      int mag = abs(gx) + abs(gy);
      if (mag > 100) {
        edges[idx] = 255;
      }
    }
  }

  // 3. Initialize 3D Accumulator
  int rRange = rMax - rMin + 1;
  int *accumulator = new int[rRange * h * w];
  memset(accumulator, 0, rRange * h * w * sizeof(int));

  // 4. Gradient-assisted Voting
  for (int y = 1; y < h - 1; y++) {
    for (int x = 1; x < w - 1; x++) {
      int idx = y * w + x;
      if (edges[idx] == 255) {
        int gx = dx_grad[idx];
        int gy = dy_grad[idx];
        if (gx == 0 && gy == 0) continue;

        double len = std::sqrt((double)(gx * gx + gy * gy));
        if (len == 0.0) continue;

        double ux = gx / len;
        double uy = gy / len;

        // 3 directions: 0 deg, +10 deg, -10 deg
        // cos(10 deg) = 0.98480775, sin(10 deg) = 0.17364817
        const double c10 = 0.98480775;
        const double s10 = 0.17364817;

        double dirs[3][2] = {
          { ux, uy },
          { ux * c10 - uy * s10, ux * s10 + uy * c10 },
          { ux * c10 + uy * s10, -ux * s10 + uy * c10 }
        };

        for (int d_idx = 0; d_idx < 3; d_idx++) {
          double dx_dir = dirs[d_idx][0];
          double dy_dir = dirs[d_idx][1];

          for (int r = rMin; r <= rMax; r++) {
            int rIdx = r - rMin;

            // Direction 1
            int a1 = (int)(x + r * dx_dir + (dx_dir >= 0 ? 0.5 : -0.5));
            int b1 = (int)(y + r * dy_dir + (dy_dir >= 0 ? 0.5 : -0.5));
            if (a1 >= 0 && a1 < w && b1 >= 0 && b1 < h) {
              accumulator[rIdx * h * w + b1 * w + a1]++;
            }

            // Direction 2 (Opposite)
            int a2 = (int)(x - r * dx_dir + (-dx_dir >= 0 ? 0.5 : -0.5));
            int b2 = (int)(y - r * dy_dir + (-dy_dir >= 0 ? 0.5 : -0.5));
            if (a2 >= 0 && a2 < w && b2 >= 0 && b2 < h) {
              accumulator[rIdx * h * w + b2 * w + a2]++;
            }
          }
        }
      }
    }
  }

  // 5. Peak Detection (3D Local Maxima)
  std::vector<DetectedCircle> candidates;
  for (int r = rMin; r <= rMax; r++) {
    int rIdx = r - rMin;
    for (int y = 2; y < h - 2; y++) {
      for (int x = 2; x < w - 2; x++) {
        int accIdx = rIdx * h * w + y * w + x;
        int votes = accumulator[accIdx];
        if (votes >= houghThreshold) {
          bool is_local_max = true;
          for (int dr = -1; dr <= 1; dr++) {
            int nrIdx = rIdx + dr;
            if (nrIdx < 0 || nrIdx >= rRange) continue;

            for (int dy = -2; dy <= 2; dy++) {
              for (int dx = -2; dx <= 2; dx++) {
                if (dr == 0 && dy == 0 && dx == 0) continue;

                int neighbor_votes = accumulator[nrIdx * h * w + (y + dy) * w + (x + dx)];
                if (neighbor_votes > votes) {
                  is_local_max = false;
                  break;
                }
                if (neighbor_votes == votes) {
                  // Break tie by coordinates
                  if (nrIdx < rIdx || (nrIdx == rIdx && (y + dy) < y) ||
                      (nrIdx == rIdx && (y + dy) == y && (x + dx) < x)) {
                    is_local_max = false;
                    break;
                  }
                }
              }
              if (!is_local_max) break;
            }
            if (!is_local_max) break;
          }

          if (is_local_max) {
            DetectedCircle circle;
            circle.a = x;
            circle.b = y;
            circle.r = r;
            circle.votes = votes;
            candidates.push_back(circle);
          }
        }
      }
    }
  }

  // 6. Circle Suppression (Filter close overlapping circles)
  std::sort(candidates.begin(), candidates.end(), compareCircles);
  std::vector<DetectedCircle> final_circles;

  for (const auto &cand : candidates) {
    bool keep = true;
    for (const auto &kept : final_circles) {
      double dist = sqrt((double)((cand.a - kept.a) * (cand.a - kept.a) +
                                  (cand.b - kept.b) * (cand.b - kept.b)));
      int max_r = (cand.r > kept.r) ? cand.r : kept.r;
      if (dist < max_r * 0.5 && abs(cand.r - kept.r) < 15) {
        keep = false;
        break;
      }
    }
    if (keep) {
      final_circles.push_back(cand);
    }
  }

  // 7. Copy original input f to output g
  int total_elements = total_pixels * d;
  for (int i = 0; i < total_elements; i++) {
    g[i] = f[i];
  }

  // 8. Draw detected circles with custom color
  for (const auto &circle : final_circles) {
    draw_circle(g, w, h, d, circle.a, circle.b, circle.r, lineR, lineG, lineB);
  }

  // 9. Cleanup
  delete[] gray;
  delete[] edges;
  delete[] dx_grad;
  delete[] dy_grad;
  delete[] accumulator;
}
}
