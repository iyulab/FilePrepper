# FilePrepper Scripts

빌드 및 테스트 자동화 스크립트 모음

## 📜 Available Scripts

### test-all.ps1

전체 빌드 및 테스트를 수행하고 결과를 요약합니다.

#### 기본 사용법

```powershell
# 기본 실행 (clean + restore + build + test)
./scripts/test-all.ps1

# 또는 pwsh 사용
pwsh -File scripts/test-all.ps1
```

#### 옵션

```powershell
# Clean 단계 건너뛰기 (빠른 실행)
./scripts/test-all.ps1 -SkipClean

# 상세 출력 (빌드 및 테스트 전체 로그)
./scripts/test-all.ps1 -Verbose

# 코드 커버리지 생성
./scripts/test-all.ps1 -Coverage

# 옵션 조합
./scripts/test-all.ps1 -SkipClean -Coverage
```

#### 출력 예시

```
╔═══════════════════════════════════════════════════════════╗
║       FilePrepper - Build & Test Suite                   ║
╚═══════════════════════════════════════════════════════════╝

🧹 Step 1/4: Cleaning previous builds...
✓ Clean completed

📦 Step 2/4: Restoring packages...
✓ Restore completed

🔨 Step 3/4: Building solution...
✓ Build successful

🧪 Step 4/4: Running tests...
✓ All tests passed

╔═══════════════════════════════════════════════════════════╗
║                    SUMMARY REPORT                         ║
╚═══════════════════════════════════════════════════════════╝

📊 Build Results:
   ✓ Status: SUCCESS
   • Warnings: 0
   • Errors: 0

🧪 Test Results:
   ✓ Status: SUCCESS
   • Total:   203 tests
   • Passed:  203 tests
   • Failed:  0 tests
   • Pass Rate: 100%

⏱️  Total Duration: 00:16.100

════════════════════════════════════════════════════════════
  🎉 ALL CHECKS PASSED - Ready for deployment!
════════════════════════════════════════════════════════════
```

#### Exit Codes

- `0`: 모든 검사 통과 (빌드 및 테스트 성공)
- `1`: 검사 실패 (빌드 실패 또는 테스트 실패)

#### CI/CD 통합 예시

```yaml
# GitHub Actions
- name: Run Tests
  run: pwsh -File scripts/test-all.ps1

# Azure Pipelines
- script: pwsh -File scripts/test-all.ps1
  displayName: 'Build and Test'
```

## 🔧 개발 환경 요구사항

- PowerShell 7+ (`pwsh`)
- .NET 9.0 SDK
- Windows, Linux, macOS 지원

## 📊 커버리지 리포트

`-Coverage` 옵션 사용 시 `TestResults/` 디렉토리에 커버리지 리포트가 생성됩니다.

```powershell
# 커버리지 생성
./scripts/test-all.ps1 -Coverage

# 리포트 확인
# TestResults/ 디렉토리에서 coverage.cobertura.xml 파일 확인
```

## 💡 팁

### 빠른 개발 사이클

```powershell
# 개발 중에는 SkipClean 사용으로 시간 단축
./scripts/test-all.ps1 -SkipClean
```

### 문제 해결

```powershell
# 빌드나 테스트 실패 시 상세 로그 확인
./scripts/test-all.ps1 -Verbose
```

### CI 환경

```powershell
# CI에서는 전체 clean 빌드 권장
./scripts/test-all.ps1 -Coverage
```
