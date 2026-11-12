from PySide6.QtGui import QImage

img7 = QImage(r'C:\AIGRAV\benchmarks\type36\images\page_07.png')
w = img7.width()
h = img7.height()
print('Page 7 size:', w, h)
img7.copy(0, 0, int(w*0.35), h).save(r'C:\AIGRAV\benchmarks\type36\images\p07_left.png')
img7.copy(int(w*0.3), 0, int(w*0.4), h).save(r'C:\AIGRAV\benchmarks\type36\images\p07_center.png')
img7.copy(int(w*0.65), 0, int(w*0.35), h).save(r'C:\AIGRAV\benchmarks\type36\images\p07_right.png')
print('Page 7 slices saved.')

img10 = QImage(r'C:\AIGRAV\benchmarks\type36\images\page_10.png')
w10 = img10.width()
h10 = img10.height()
img10.copy(0, 0, int(w10*0.5), h10).save(r'C:\AIGRAV\benchmarks\type36\images\p10_left.png')
img10.copy(int(w10*0.5), 0, int(w10*0.5), h10).save(r'C:\AIGRAV\benchmarks\type36\images\p10_right.png')
print('Page 10 slices saved.')
