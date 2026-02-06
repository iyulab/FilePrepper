# FilePrepper 개발 작업 관리

> **최종 업데이트**: 2026-02-06
> **현재 버전**: v0.4.9
> **다음 목표**: Phase 3 Planning

---

## 📊 현재 상태

### ✅ 최근 완료 작업

#### Phase 2: Advanced Data Transformations (2025-01-09 완료) ⭐
**목표**: 고급 데이터 변환 기능 추가
**상태**: ✅ 5/5 기능 100% 완료

**구현된 기능**:
1. ✅ **DateTime Operations** - 파싱, 포맷 변환, 컴포넌트 추출 (20배 성능 향상)
2. ✅ **Merge-As-Of Operations** - 시계열 데이터 tolerance 조인
3. ✅ **String Operations** - 문자열 변환, substring, 연결
4. ✅ **Conditional Operations** - 조건부 컬럼 생성 (if-then-else)
5. ✅ **Window Operations** - Resample (시간 기반), Rolling (행 기반) 집계 ⭐ NEW

**주요 성과**:
- DateTime: 20배 성능 향상
- Window: 61% 데이터 감소 (Dataset 003: 32K → 12K rows)
- 실전 검증: Dataset 001, 002, 003, 005, 006 워크플로우 완성
- 코드 품질: Clean architecture, 프로덕션 레디

**문서**:
- `docs/FILEPREPPER_PHASE2_COMPLETE.md` - 종합 가이드
- `examples/Preprocessor006/` - Python 통합 예제
- Dataset 003 워크플로우 스크립트

**커밋**:
- Git commit: `1770b32` (2025-01-09)
- 42개 파일, 5,811줄 추가

---

#### Phase 1: CLI 리팩토링 (2025-01-04 완료)
- ✅ `CommandLineParser` → `System.CommandLine` 마이그레이션
- ✅ `Spectre.Console` 통합 (리치 터미널 UI)
- ✅ `BaseCommand` 인프라 구현
- ✅ 26개 명령어 전체 마이그레이션 완료
- ✅ 버전 정보 표시 (`-v` 플래그)
- ✅ 멀티 포맷 지원 강조 (CSV, TSV, JSON, XML, Excel)

---

## 🎯 다음 단계 옵션

### Option 1: MLoop 프로젝트 복귀 (권장)
**이유**: FilePrepper Phase 2 완료, MLoop에서 활용 가능
**작업**:
- MLoop Phase 2 진행
- 데이터셋 전처리 FilePrepper로 통합
- 워크플로우 완성 및 검증

### Option 2: FilePrepper Phase 3 계획
**Feature Engineering 확장**:
- [ ] Polynomial features (다항식 피처)
- [ ] Interaction terms (상호작용항)
- [ ] Binning/discretization (구간화)

**Data Quality 강화**:
- [ ] Outlier detection (이상치 탐지)
- [ ] Data profiling (데이터 프로파일링)
- [ ] Schema validation (스키마 검증)

**ML Integration**:
- [ ] Auto-feature selection (자동 피처 선택)
- [ ] Dataset versioning (데이터셋 버전 관리)
- [ ] Experiment tracking (실험 추적)

### Option 3: 성능 최적화
- [ ] Parallel processing (병렬 처리)
- [ ] Streaming support (스트리밍 처리)
- [ ] Large dataset optimization (대용량 데이터 최적화)

### Option 4: Window Operations 확장
- [ ] Additional aggregation methods (median, percentile)
- [ ] EWMA (지수 가중 이동 평균)
- [ ] Gap handling strategies (간격 처리 전략)
- [ ] Seasonal decomposition (계절성 분해)

---

## 📋 전체 명령어 목록 (29개)

### Phase 1 명령어 (20개)
1. normalize-data
2. scale-data
3. one-hot-encoding
4. data-type-convert
5. date-extraction
6. fill-missing-values
7. drop-duplicates
8. value-replace
9. add-columns
10. remove-columns
11. rename-columns
12. reorder-columns
13. column-interaction
14. basic-statistics
15. aggregate
16. filter-rows
17. merge
18. data-sampling
19. file-format-convert
20. create-lag-features

### Phase 2 추가 명령어 (6개)
21. **datetime** - DateTime 파싱 및 변환
22. **merge-asof** - 시계열 데이터 조인
23. **string** - 문자열 변환 작업
24. **conditional** - 조건부 컬럼 생성
25. **unpivot** - Wide → Long 형식 변환
26. **window** - Resample/Rolling 집계

### v0.4.9 추가 명령어 (3개)
27. **csv-cleaner** - CSV 파일 정리
28. **expression** - 수식 기반 컬럼 생성
29. **remove-constants** - 상수/준상수 컬럼 제거

---

## 💡 참고 정보

### Window Operations 사용 예시
```bash
# Resample: 5분 윈도우 집계
fileprepper window \
    -i sensor_data.csv -o aggregated.csv \
    --type resample --method mean \
    --columns temperature,humidity \
    --time-column timestamp --window 5T --header

# Rolling: 3-row 이동 평균
fileprepper window \
    -i sensor_data.csv -o rolling.csv \
    --type rolling --method mean \
    --columns temperature,humidity \
    --window-size 3 --suffix "_3roll" --header
```

### Dataset 003 워크플로우
```bash
# 프레스 전류 데이터 5분 집계
bash scripts/preprocess_press_data.sh

# 또는 Python
python examples/Preprocessor006/preprocess_dataset_006.py
```

---

**현재 상태: v0.4.9** 🎉

FilePrepper는 29개 명령어, 자동 인코딩 감지, glob 병합, 상수 컬럼 제거를 지원하는 완전한 ML 데이터 전처리 도구입니다.
