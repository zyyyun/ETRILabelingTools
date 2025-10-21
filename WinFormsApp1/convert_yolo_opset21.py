#!/usr/bin/env python3
"""
YOLOv8 ONNX 모델을 Opset 21로 변환하는 스크립트
"""

import os
import sys

def convert_yolo_to_opset21():
    try:
        # ultralytics 라이브러리 import
        from ultralytics import YOLO
        
        print("YOLOv8 모델을 Opset 21로 변환 중...")
        
        # 현재 디렉토리에서 yolov8n.pt 파일 찾기
        model_path = None
        possible_paths = [
            "yolov8n.pt",
            "../yolov8n.pt", 
            "../../yolov8n.pt",
            "yolov8n.onnx"  # 기존 ONNX 파일이 있다면
        ]
        
        for path in possible_paths:
            if os.path.exists(path):
                model_path = path
                break
        
        if not model_path:
            print("❌ YOLOv8 모델 파일을 찾을 수 없습니다.")
            print("다음 중 하나의 파일이 필요합니다:")
            print("- yolov8n.pt (PyTorch 모델)")
            print("- yolov8n.onnx (ONNX 모델)")
            return False
        
        print(f"📁 모델 파일 발견: {model_path}")
        
        # YOLO 모델 로드
        model = YOLO(model_path)
        
        # Opset 21로 ONNX 변환
        print("🔄 Opset 21로 변환 중...")
        model.export(
            format='onnx',
            opset=21,
            simplify=True,
            dynamic=False,
            imgsz=640
        )
        
        print("✅ 변환 완료!")
        print("📄 변환된 파일: yolov8n.onnx")
        print("이제 프로그램을 다시 실행해보세요.")
        
        return True
        
    except ImportError:
        print("❌ ultralytics 라이브러리가 설치되지 않았습니다.")
        print("다음 명령어로 설치하세요:")
        print("pip install ultralytics")
        return False
        
    except Exception as e:
        print(f"❌ 변환 중 오류 발생: {e}")
        return False

if __name__ == "__main__":
    print("=" * 50)
    print("YOLOv8 ONNX Opset 21 변환기")
    print("=" * 50)
    
    success = convert_yolo_to_opset21()
    
    if success:
        print("\n🎉 변환이 성공적으로 완료되었습니다!")
    else:
        print("\n💥 변환에 실패했습니다.")
        print("수동으로 다음 명령어를 실행해보세요:")
        print("python -c \"from ultralytics import YOLO; YOLO('yolov8n.pt').export(format='onnx', opset=21)\"")
    
    input("\n엔터를 눌러 종료...")


