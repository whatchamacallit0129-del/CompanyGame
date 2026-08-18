# Company Game Command Agent

## 목적

`CompanyGameCommandAgent`는 GitHub의 `command.json`을 통해 Unity Editor에서 오브젝트를 생성·삭제·수정할 수 있도록 하는 명령 실행 시스템이다.

핵심 방향은 **하드코딩을 최소화하고, 명령을 조합해서 다양한 Unity 작업을 수행할 수 있는 소프트코딩 구조**를 유지하는 것이다.

---

## 현재 지원 명령

### 오브젝트 생성

```text
CREATE_INTERACTABLE_OBJECT:직원:5
```

지정된 타입의 상호작용 오브젝트를 여러 개 생성한다.

```text
CREATE_EMPTY_OBJECT:이름
CREATE_OBJECT:이름
```

빈 GameObject를 생성한다.

### 삭제

```text
DELETE_OBJECT:직원:8
```

지정 개수를 삭제한다.

**번호가 붙은 오브젝트는 높은 번호부터 삭제한다.**

예: 직원 1~16이 있고 8개 삭제 →

```text
16 → 15 → 14 → 13 → 12 → 11 → 10 → 9
```

따라서 낮은 번호의 기존 오브젝트를 최대한 보존한다.

### 이름 변경

```text
RENAME_OBJECT:직원 (1):카르멘
```

### GameObject 활성/비활성

```text
SET_ACTIVE:카르멘:false
SET_ACTIVE:카르멘:true
```

### Transform

```text
SET_POSITION:카르멘:3:1:2
SET_SCALE:카르멘:1:1:1
SET_ROTATION:카르멘:0:0:0
```

### 부모 설정

```text
SET_PARENT:카르멘:직원관리
```

### 컴포넌트 추가/제거

```text
ADD_COMPONENT:카르멘:BoxCollider2D
REMOVE_COMPONENT:카르멘:BoxCollider2D
```

### 개별 컴포넌트 활성/비활성

GameObject 전체가 아니라 Inspector의 특정 컴포넌트만 제어한다.

```text
SET_COMPONENT_ACTIVE:카르멘:BoxCollider2D:false
SET_COMPONENT_ACTIVE:카르멘:BoxCollider2D:true
```

### 컴포넌트 속성 수정

Inspector에 직렬화된 속성을 가능한 한 범용적으로 수정한다.

```text
SET_COMPONENT_PROPERTY:카르멘:BoxCollider2D:size:2,3
SET_COMPONENT_PROPERTY:카르멘:BoxCollider2D:offset:1,0
```

지원 대상은 특정 컴포넌트에 한정하지 않고 Unity의 `SerializedProperty` 타입을 기준으로 확장한다.

---

## 소프트코딩 설계 원칙

1. **컴포넌트별 전용 명령을 계속 만들지 않는다.**
   - `BoxCollider2D` 전용 함수, `Rigidbody2D` 전용 함수 등을 무한히 추가하지 않는다.
   - 가능한 경우 `SerializedObject` / `SerializedProperty` 기반으로 Inspector 필드를 동적으로 탐색한다.

2. **명령어와 Unity 구현을 분리한다.**
   - 명령 파싱
   - 대상 GameObject 탐색
   - 컴포넌트 탐색
   - 속성 변환
   - Unity 값 적용
   을 독립적으로 유지한다.

3. **새 컴포넌트를 추가해도 기존 Command Agent를 수정하지 않는 것을 목표로 한다.**

4. **실패 원인을 명확하게 반환한다.**
   - 대상 없음
   - 컴포넌트 없음
   - 속성 없음
   - 지원하지 않는 타입
   - 값 변환 실패

5. **파괴적인 명령은 신중하게 처리한다.**
   - 삭제 대상이 없을 때 조용히 성공 처리하지 않는다.
   - 삭제 수량과 실제 삭제 수량을 결과에 기록한다.

---

## 향후 추가하면 좋은 기능

### 1. Inspector 속성 읽기

현재 수정뿐 아니라 **현재 값을 읽는 명령**이 필요하다.

예:

```text
GET_COMPONENT_PROPERTY:카르멘:BoxCollider2D:size
```

→ 현재 Size를 결과로 반환.

이 기능이 있으면 ChatGPT가 먼저 현재 상태를 확인하고 필요한 변경만 명령할 수 있다.

### 2. GameObject/컴포넌트 상태 조회

```text
GET_OBJECT_INFO:카르멘
GET_COMPONENTS:카르멘
```

오브젝트의 Transform, 컴포넌트 목록, 활성 상태 등을 반환한다.

### 3. 범용 Inspector 타입 지원 확대

우선순위:

- bool
- int
- float
- string
- enum
- Vector2
- Vector3
- Vector4
- Color
- Object reference
- LayerMask
- AnimationCurve
- Gradient
- 배열 / 리스트

특히 Sprite, Material, ScriptableObject 같은 **Unity Object Reference를 이름/경로/GUID 등으로 안전하게 지정하는 기능**이 중요하다.

### 4. 자식/컴포넌트 경로 지정

동일한 컴포넌트가 여러 개 있거나 자식 오브젝트를 조작할 수 있도록 한다.

예:

```text
SET_COMPONENT_PROPERTY:카르멘/몸:SpriteRenderer:sortingOrder:5
```

### 5. Undo/Redo 지원

Command Agent가 실행한 작업을 Unity Undo 시스템에 등록한다.

```text
UNDO_LAST_COMMAND
REDO_LAST_COMMAND
```

개발 단계에서 매우 중요하다.

### 6. 명령 배치 실행

여러 명령을 하나의 작업으로 묶는다.

예:

```text
BEGIN_BATCH:카르멘수정
SET_POSITION:카르멘:3:1:2
SET_COMPONENT_ACTIVE:카르멘:BoxCollider2D:false
SET_ACTIVE:카르멘:true
END_BATCH
```

실패 시 전체를 되돌리는 트랜잭션 방식도 고려한다.

### 7. 조건부 명령

현재 상태에 따라 명령을 실행한다.

예:

```text
IF_EXISTS:카르멘:SET_ACTIVE:카르멘:true
```

또는 조회 결과를 기반으로 후속 작업을 실행할 수 있도록 한다.

### 8. 프리팹/에셋 제어

향후에는 Scene 오브젝트뿐 아니라:

- Prefab 생성/수정
- Prefab 인스턴스 교체
- Sprite/Material 지정
- ScriptableObject 생성/수정
- 에셋 경로 조회

까지 확장할 수 있다.

### 9. 씬 제어

```text
CREATE_SCENE:이름
SAVE_SCENE
LOAD_SCENE:이름
```

단, 씬 저장/삭제는 데이터 손실 가능성이 있으므로 별도의 안전장치를 둔다.

### 10. 명령 결과 표준화

모든 명령은 다음과 같은 구조의 결과를 남기는 것을 목표로 한다.

```text
SUCCESS
COMMAND: SET_COMPONENT_PROPERTY
TARGET: 카르멘
COMPONENT: BoxCollider2D
PROPERTY: size
OLD_VALUE: 1,1
NEW_VALUE: 2,3
```

실패도 사람이 이해할 수 있는 이유와 함께 반환한다.

---

## 추천 개발 우선순위

### Phase 1 — 기본 편집

- [x] 생성
- [x] 삭제
- [x] 이름 변경
- [x] 활성/비활성
- [x] Position / Rotation / Scale
- [x] Parent
- [x] Component 추가/제거
- [x] 개별 Component 활성/비활성
- [x] 범용 Component Property 수정

### Phase 2 — 상태 확인

- [ ] Object 정보 조회
- [ ] Component 목록 조회
- [ ] Component Property 읽기
- [ ] 변경 전/후 값 기록

### Phase 3 — 안전성

- [ ] Unity Undo 연동
- [ ] 명령 결과 표준화
- [ ] Batch / Transaction
- [ ] 실패 시 자동 복구

### Phase 4 — 고급 Unity 자동화

- [ ] Object Reference 수정
- [ ] Prefab 제어
- [ ] Sprite / Material / Animator 제어
- [ ] ScriptableObject 제어
- [ ] Scene 제어
- [ ] Asset 관리

---

## 핵심 목표

최종적으로는 다음과 같은 흐름을 목표로 한다.

```text
사용자 자연어
    ↓
ChatGPT
    ↓
Command Agent 명령
    ↓
command.json
    ↓
Unity Command Agent
    ↓
GameObject / Component / Inspector
    ↓
result.json
    ↓
ChatGPT가 실행 결과 확인
```

궁극적으로는 **"Unity에서 사람이 Inspector를 직접 클릭해서 할 수 있는 작업의 대부분을 자연어 명령으로 수행"**할 수 있는 범용 Unity 작업 에이전트를 만드는 것을 목표로 한다.
