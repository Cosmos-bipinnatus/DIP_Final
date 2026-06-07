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
  if (f == nullptr || g == nullptr || w <= 0 || h <= 0 || d <= 0) {
    return;
  }

  int total_pixels = w * h;
  unsigned char *gray = new unsigned char[total_pixels];

  // Convert to grayscale if needed
  for (int i = 0; i < total_pixels; i++) {
    if (d == 1) {
      int v = f[i];
      if (v < 0)
        v = 0;
      if (v > 255)
        v = 255;
      gray[i] = (unsigned char)v;
    } else {
      int idx = i * d;
      double b = f[idx + 0];
      double gval = f[idx + 1];
      double r = f[idx + 2];
      int gray_val = (int)(r * 0.299 + gval * 0.587 + b * 0.114 + 0.5);
      if (gray_val < 0)
        gray_val = 0;
      if (gray_val > 255)
        gray_val = 255;
      gray[i] = (unsigned char)gray_val;
    }
  }

  // Sobel operator (simple magnitude = |gx| + |gy|)
  for (int y = 1; y < h - 1; y++) {
    for (int x = 1; x < w - 1; x++) {
      int idx = y * w + x;
      int gx = -gray[(y - 1) * w + (x - 1)] + gray[(y - 1) * w + (x + 1)] -
               2 * gray[y * w + (x - 1)] + 2 * gray[y * w + (x + 1)] -
               gray[(y + 1) * w + (x - 1)] + gray[(y + 1) * w + (x + 1)];

      int gy = -gray[(y - 1) * w + (x - 1)] - 2 * gray[(y - 1) * w + x] -
               gray[(y - 1) * w + (x + 1)] + gray[(y + 1) * w + (x - 1)] +
               2 * gray[(y + 1) * w + x] + gray[(y + 1) * w + (x + 1)];

      int mag = abs(gx) + abs(gy);
      if (mag > 255)
        mag = 255;
      if (mag < 0)
        mag = 0;

      if (d == 1) {
        g[idx] = mag;
      } else {
        int outIdx = idx * d;
        g[outIdx + 0] = mag; // B
        g[outIdx + 1] = mag; // G
        g[outIdx + 2] = mag; // R
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

  delete[] gray;
}

__declspec(dllexport) void detect_canny(int *f, int w, int h, int d, int *g,
                                        double low_threshold,
                                        double high_threshold) {
  if (f == nullptr || g == nullptr || w <= 0 || h <= 0 || d <= 0) {
    return;
  }

  int total_pixels = w * h;
  unsigned char *gray = new unsigned char[total_pixels];

  // 1. 轉為灰階 (與你的 Sobel 完全相同)
  for (int i = 0; i < total_pixels; i++) {
    if (d == 1) {
      int v = f[i];
      if (v < 0)
        v = 0;
      if (v > 255)
        v = 255;
      gray[i] = (unsigned char)v;
    } else {
      int idx = i * d;
      double b = f[idx + 0];
      double gval = f[idx + 1];
      double r = f[idx + 2];
      int gray_val = (int)(r * 0.299 + gval * 0.587 + b * 0.114 + 0.5);
      if (gray_val < 0)
        gray_val = 0;
      if (gray_val > 255)
        gray_val = 255;
      gray[i] = (unsigned char)gray_val;
    }
  }

  // 分配 Canny 專用的內部暫存空間
  unsigned char *blurred = new unsigned char[total_pixels];
  double *grad_mag = new double[total_pixels];
  double *grad_dir = new double[total_pixels]; // 儲存角度 (弳度)
  unsigned char *nms = new unsigned char[total_pixels];

  // 2. 高斯濾波 (Gaussian Blur 3x3) 降噪
  // 權重矩陣: [1 2 1; 2 4 2; 1 2 1] / 16
  for (int y = 0; y < h; y++) {
    for (int x = 0; x < w; x++) {
      if (y == 0 || y == h - 1 || x == 0 || x == w - 1) {
        blurred[y * w + x] = gray[y * w + x];
        continue;
      }
      int sum = gray[(y - 1) * w + (x - 1)] * 1 + gray[(y - 1) * w + x] * 2 +
                gray[(y - 1) * w + (x + 1)] * 1 + gray[y * w + (x - 1)] * 2 +
                gray[y * w + x] * 4 + gray[y * w + (x + 1)] * 2 +
                gray[(y + 1) * w + (x - 1)] * 1 + gray[(y + 1) * w + x] * 2 +
                gray[(y + 1) * w + (x + 1)] * 1;
      blurred[y * w + x] = (unsigned char)(sum / 16);
    }
  }

  // 3. 計算 Sobel 梯度大小與方向
  // 初始化邊緣為 0
  for (int i = 0; i < total_pixels; i++) {
    grad_mag[i] = 0.0;
    grad_dir[i] = 0.0;
    nms[i] = 0;
  }

  for (int y = 1; y < h - 1; y++) {
    for (int x = 1; x < w - 1; x++) {
      int idx = y * w + x;
      int gx = -blurred[(y - 1) * w + (x - 1)] +
               blurred[(y - 1) * w + (x + 1)] - 2 * blurred[y * w + (x - 1)] +
               2 * blurred[y * w + (x + 1)] - blurred[(y + 1) * w + (x - 1)] +
               blurred[(y + 1) * w + (x + 1)];

      int gy = -blurred[(y - 1) * w + (x - 1)] - 2 * blurred[(y - 1) * w + x] -
               blurred[(y - 1) * w + (x + 1)] + blurred[(y + 1) * w + (x - 1)] +
               2 * blurred[(y + 1) * w + x] + blurred[(y + 1) * w + (x + 1)];

      grad_mag[idx] = std::sqrt(gx * gx + gy * gy);
      grad_dir[idx] = std::atan2(gy, gx); // 範圍 -PI 到 PI
    }
  }

  // 4. 非極大值抑制 (NMS) - 邊緣削薄
  for (int y = 1; y < h - 1; y++) {
    for (int x = 1; x < w - 1; x++) {
      int idx = y * w + x;
      double mag = grad_mag[idx];
      if (mag == 0)
        continue;

      // 將角度轉換為 0, 45, 90, 135 四個方向
      double angle = grad_dir[idx] * 180.0 / 3.141592653589793;
      if (angle < 0)
        angle += 180.0;

      double mag1 = 0, mag2 = 0;

      if ((angle >= 0 && angle < 22.5) || (angle >= 157.5 && angle <= 180)) {
        // 水平方向 (檢查左右像素)
        mag1 = grad_mag[y * w + (x - 1)];
        mag2 = grad_mag[y * w + (x + 1)];
      } else if (angle >= 22.5 && angle < 67.5) {
        // 45度對角線 (檢查右上、左下)
        mag1 = grad_mag[(y - 1) * w + (x + 1)];
        mag2 = grad_mag[(y + 1) * w + (x - 1)];
      } else if (angle >= 67.5 && angle < 112.5) {
        // 垂直方向 (檢查上下像素)
        mag1 = grad_mag[(y - 1) * w + x];
        mag2 = grad_mag[(y + 1) * w + x];
      } else if (angle >= 112.5 && angle < 157.5) {
        // 135度對角線 (檢查左上、右下)
        mag1 = grad_mag[(y - 1) * w + (x - 1)];
        mag2 = grad_mag[(y + 1) * w + (x + 1)];
      }

      // 只有當自己是區域內最大值時才保留
      if (mag >= mag1 && mag >= mag2) {
        nms[idx] = (mag > 255) ? 255 : (unsigned char)mag;
      } else {
        nms[idx] = 0;
      }
    }
  }

  // 5. 雙門檻值與滯後邊緣追蹤 (Double Threshold & Hysteresis)
  // 定義狀態：0 = 沒邊緣, 1 = 弱邊緣(待確認), 2 = 強邊緣
  unsigned char *edge_status = new unsigned char[total_pixels];
  int *stack = new int[total_pixels]; // 用陣列模擬實作堆疊(Stack)，避免遞迴造成
                                      // Stack Overflow
  int stack_top = 0;

  for (int i = 0; i < total_pixels; i++) {
    if (nms[i] >= high_threshold) {
      edge_status[i] = 2;     // 強邊緣
      stack[stack_top++] = i; // 將強邊緣座標壓入堆疊
    } else if (nms[i] >= low_threshold) {
      edge_status[i] = 1; // 弱邊緣
    } else {
      edge_status[i] = 0;
    }
  }

  // 連通性追蹤：透過強邊緣延伸去救援鄰近的弱邊緣
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
        // 如果鄰居是弱邊緣，它被認可升格為強邊緣，並繼續向外追蹤
        if (edge_status[neighbor_idx] == 1) {
          edge_status[neighbor_idx] = 2;
          stack[stack_top++] = neighbor_idx;
        }
      }
    }
  }

  // 6. 輸出結果與邊緣清理 (對齊你的輸出格式)
  for (int y = 0; y < h; y++) {
    for (int x = 0; x < w; x++) {
      int idx = y * w + x;

      // 處理邊界或是未被救援成功的弱邊緣，一律設為 0
      int final_val = 0;
      if (y > 0 && y < h - 1 && x > 0 && x < w - 1) {
        if (edge_status[idx] == 2) {
          final_val = 255; // 輸出的 Canny 邊緣通常是純白(255)
        }
      }

      // 寫入最終輸出陣列 g
      if (d == 1) {
        g[idx] = final_val;
      } else {
        int outIdx = idx * d;
        g[outIdx + 0] = final_val; // B
        g[outIdx + 1] = final_val; // G
        g[outIdx + 2] = final_val; // R
        // 如果有 Alpha 通道 (d == 4)，保留原本的值
        if (d == 4)
          g[outIdx + 3] = f[outIdx + 3];
      }
    }
  }

  // 7. 釋放記憶體，防止記憶體洩漏 (Memory Leak)
  delete[] gray;
  delete[] blurred;
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
