document.addEventListener('DOMContentLoaded', () => {
    
    // Lucide 아이콘 초기화
    if (typeof lucide !== 'undefined') {
        lucide.createIcons();
    }

    // --- 1. 탭/페이지 전환 로직 ---
    const navLinks = document.querySelectorAll('.nav-link-header, .nav-link');
    const pages = document.querySelectorAll('.page');
    const mobileMenu = document.getElementById('mobile-menu');
    const menuButton = document.getElementById('mobile-menu-button');
    const openIcon = document.getElementById('menu-open-icon');
    const closeIcon = document.getElementById('menu-close-icon');

    /**
     * 페이지를 전환하고, 탭 상태를 업데이트하며, 모바일 메뉴를 닫습니다.
     * @param {string} pageId - 활성화할 페이지의 ID (예: 'page-main')
     */
    function switchPage(pageId) {
        // 모든 페이지 숨기기
        pages.forEach(page => {
            page.classList.remove('page-active');
        });
        
        // 모든 탭 비활성화
        navLinks.forEach(link => {
            link.classList.remove('tab-active');
        });

        // 대상 페이지 보이기
        const targetPage = document.getElementById(pageId);
        if (targetPage) {
            targetPage.classList.add('page-active');
        }

        // 대상 탭 활성화
        const activeLinks = document.querySelectorAll(`.nav-link[data-page="${pageId}"], .nav-link-header[data-page="${pageId}"]`);
        activeLinks.forEach(link => {
            link.classList.add('tab-active');
        });
        
        // 모바일 메뉴 닫기
        if (!mobileMenu.classList.contains('hidden')) {
            toggleMobileMenu();
        }

        // 페이지 상단으로 스크롤
        window.scrollTo(0, 0);
    }

    // 네비게이션 링크 클릭 이벤트 리스너 등록
    navLinks.forEach(link => {
        link.addEventListener('click', (e) => {
            e.preventDefault();
            const pageId = link.getAttribute('data-page');
            switchPage(pageId);
        });
    });

    // 기본 페이지 설정 (메인)
    switchPage('page-main');

    // --- 2. 모바일 메뉴 토글 로직 ---
    function toggleMobileMenu() {
        mobileMenu.classList.toggle('hidden');
        openIcon.classList.toggle('hidden');
        closeIcon.classList.toggle('hidden');
    }

    menuButton.addEventListener('click', toggleMobileMenu);

    // --- 3. '게임 소개' 기능 카드 + 모달 로직 ---

    // 소개 항목 정의. 새 항목 추가 시 여기에만 추가하면 됩니다.
    // media: null이면 "준비 중" 표시, 추후 { type: 'video'|'image', src: '...' } 지정
    const FEATURES = [
        {
            icon: '🃏',
            title: '전략적인 덱 빌딩',
            tagline: '다양한 학교와 동아리 소속 학생들로 자신만의 최강 덱을 구성하세요.',
            desc: `
                <p>'카드아카이브'에서는 각기 다른 스킬과 코스트를 가진 수백 종의 카드를 만나볼 수 있습니다.
                아비도스, 게헨나, 트리니티, 밀레니엄 등 익숙한 학원 학생들을 조합하여
                강력한 시너지를 발휘하는 덱을 만들 수 있습니다.</p>
                <ul>
                    <li>100종 이상의 유니크한 학생 카드</li>
                    <li>다양한 전략을 가능하게 하는 스펠 카드</li>
                    <li>학원/동아리별 고유 시너지 효과</li>
                </ul>`,
            media: null,
        },
        {
            icon: '⚔️',
            title: '간편하고 전략적인 전투',
            tagline: '매 턴 주어지는 코스트를 활용하여 최적의 판단을 내려야 합니다.',
            desc: `
                <p>전투는 턴제로 진행되며, 플레이어는 매 턴 자동으로 회복되는 '코스트'를 사용하여
                카드를 필드에 내거나 스킬을 사용할 수 있습니다.
                상대방의 전략을 예측하고, 학생들의 고유 스킬을 적재적소에 활용하여 전투를 승리로 이끄세요.</p>
                <ul>
                    <li>직관적인 드래그 앤 드롭 조작</li>
                    <li>코스트 기반의 실시간 전략 판단</li>
                    <li>학생 고유의 EX 스킬 구현</li>
                </ul>`,
            media: null,
        },
    ];

    function initFeatureCards() {
        const featureList = document.getElementById('feature-list');
        const modal = document.getElementById('feature-modal');
        if (!featureList || !modal) return;

        const mediaEl = document.getElementById('feature-modal-media');
        const titleEl = document.getElementById('feature-modal-title');
        const taglineEl = document.getElementById('feature-modal-tagline');
        const descEl = document.getElementById('feature-modal-desc');

        function openModal(feature) {
            titleEl.textContent = feature.title;
            taglineEl.textContent = feature.tagline;
            descEl.innerHTML = feature.desc;

            // 좌측 미디어: 영상/GIF는 추후 추가 예정
            mediaEl.innerHTML = '';
            if (feature.media && feature.media.type === 'video') {
                const video = document.createElement('video');
                video.src = feature.media.src;
                video.controls = true;
                video.autoplay = true;
                video.muted = true;
                video.loop = true;
                mediaEl.appendChild(video);
            } else if (feature.media && feature.media.type === 'image') {
                const img = document.createElement('img');
                img.src = feature.media.src;
                img.alt = feature.title;
                mediaEl.appendChild(img);
            } else {
                mediaEl.innerHTML = '<div class="media-placeholder">🎬<br>시연 영상 준비 중</div>';
            }

            modal.classList.remove('hidden');
            modal.setAttribute('aria-hidden', 'false');
            document.body.style.overflow = 'hidden'; // 배경 스크롤 잠금
        }

        function closeModal() {
            modal.classList.add('hidden');
            modal.setAttribute('aria-hidden', 'true');
            document.body.style.overflow = '';
        }

        // 기능 카드 생성 (가로 정렬)
        FEATURES.forEach(feature => {
            const card = document.createElement('button');
            card.type = 'button';
            card.className = 'feature-card';
            card.innerHTML = `
                <div class="feature-icon">${feature.icon}</div>
                <h3>${feature.title}</h3>
                <p>${feature.tagline}</p>
                <span class="feature-more">자세히 보기 →</span>
            `;
            card.addEventListener('click', () => openModal(feature));
            featureList.appendChild(card);
        });

        // 닫기: 사각형 바깥(딤 배경) 클릭, ESC
        document.getElementById('feature-modal-backdrop').addEventListener('click', closeModal);
        document.addEventListener('keydown', (e) => {
            if (e.key === 'Escape' && !modal.classList.contains('hidden')) closeModal();
        });
    }

    initFeatureCards();

    // --- 4. 카드 목록 및 필터링 로직 ---
    
    // !!중요!!
    // 실제 배포 시 이 MOCK_CARDS 대신 서버에서 데이터를 가져와야 합니다.
    
    let allCards = []; // 서버에서 받아온 원본 데이터를 저장할 배열

    const cardGrid = document.getElementById('card-list-grid');
    const cardCountEl = document.getElementById('card-list-count');
    
    // 필터 폼 요소
    const searchInput = document.getElementById('filter-search');
    const typeCheckboxes = document.querySelectorAll('.filter-type');
    const schoolSelect = document.getElementById('filter-school');
    const clubSelect = document.getElementById('filter-club');
    const costMinInput = document.getElementById('filter-cost-min');
    const costMaxInput = document.getElementById('filter-cost-max');
    const resetButton = document.getElementById('filter-reset');

    async function fetchCardData() {
        try {
            const response = await fetch('/cards');

            if (!response.ok) {
                throw new Error(`HTTP Error! Status: ${response.status}. 서버에서 데이터를 가져오는 데 실패했습니다.`);
            }

            allCards = await response.json(); 
        } catch (error) {
            console.error('API 호출 실패. Express 서버의 /api/cards 엔드포인트를 확인하세요:', error);
        }
    }

    // 서버 데이터에는 카드 이름이 없어 tid를 표시용 이름으로 사용
    function getCardTitle(card) {
        return (card.tid || '').replace(/_/g, ' ');
    }

    function getImageUrl(card) {
        if (!card.image) return '';
        return card.image.startsWith('/public') ? card.image.replace('/public', '') : card.image;
    }

    // --- 학원/동아리 드롭다운 (카드 데이터 기반으로 자동 구성) ---
    let academyClubs = {};   // { 학원명: Set(동아리명) }
    let allClubs = new Set(); // 전체 동아리명

    function buildFilterOptions() {
        academyClubs = {};
        allClubs = new Set();

        allCards.forEach(card => {
            const academy = card.academy || '';
            const clubs = card.club_titles || [];
            if (academy && !academyClubs[academy])
                academyClubs[academy] = new Set();
            clubs.forEach(club => {
                if (academy) academyClubs[academy].add(club);
                allClubs.add(club);
            });
        });

        populateSelect(schoolSelect, Object.keys(academyClubs).sort(), '전체 학원');
        populateSelect(clubSelect, [...allClubs].sort(), '전체 동아리');
    }

    function populateSelect(select, values, allLabel) {
        const previous = select.value;
        select.innerHTML = '';

        const allOption = document.createElement('option');
        allOption.value = '';
        allOption.textContent = allLabel;
        select.appendChild(allOption);

        values.forEach(value => {
            const option = document.createElement('option');
            option.value = value;
            option.textContent = value;
            select.appendChild(option);
        });

        // 이전 선택이 새 목록에도 있으면 유지, 없으면 '전체'로
        if ([...select.options].some(o => o.value === previous))
            select.value = previous;
    }

    // 학원 선택 시 동아리 드롭다운을 해당 학원 소속 동아리로 갱신
    function updateClubOptions() {
        const academy = schoolSelect.value;
        const clubs = academy ? [...(academyClubs[academy] || [])] : [...allClubs];
        populateSelect(clubSelect, clubs.sort(), '전체 동아리');
    }

    // --- 카드 확대 보기 (라이트박스) ---
    const cardLightbox = document.getElementById('card-lightbox');
    const cardLightboxImg = document.getElementById('card-lightbox-img');

    function openCardLightbox(card) {
        cardLightboxImg.src = getImageUrl(card);
        cardLightboxImg.alt = getCardTitle(card);
        cardLightbox.classList.remove('hidden');
        cardLightbox.setAttribute('aria-hidden', 'false');
        document.body.style.overflow = 'hidden'; // 배경 스크롤 잠금
    }

    function closeCardLightbox() {
        cardLightbox.classList.add('hidden');
        cardLightbox.setAttribute('aria-hidden', 'true');
        document.body.style.overflow = '';
    }

    // 닫기: 외곽(딤 배경) 클릭, ESC
    document.getElementById('card-lightbox-backdrop').addEventListener('click', closeCardLightbox);
    document.addEventListener('keydown', (e) => {
        if (e.key === 'Escape' && !cardLightbox.classList.contains('hidden')) closeCardLightbox();
    });

    /**
     * 카드를 화면에 렌더링하는 함수
     * @param {Array} cardsToRender - 화면에 표시할 카드 객체 배열
     */
    function renderCards(cardsToRender) {
        cardGrid.innerHTML = ''; // 기존 목록 초기화

        if (cardsToRender.length === 0) {
            cardGrid.innerHTML = '<p class="col-span-full text-center text-gray-500">일치하는 카드가 없습니다.</p>';
        } else {
            cardsToRender.forEach(card => {
                const title = getCardTitle(card);
                const image_url = getImageUrl(card);

                const cardElement = document.createElement('a');
                cardElement.href = '#';
                cardElement.className = 'group';
                cardElement.setAttribute('title', title);

                // 이미지가 없거나 로드에 실패하면 자리표시자 표시
                const makePlaceholder = () => {
                    const placeholder = document.createElement('div');
                    placeholder.className = 'card-image card-image-placeholder';
                    placeholder.textContent = title;
                    return placeholder;
                };

                if (image_url) {
                    const img = document.createElement('img');
                    img.src = image_url;
                    img.alt = title;
                    img.loading = 'lazy';
                    img.className = 'card-image w-full rounded-lg shadow-sm transition-all group-hover:shadow-xl group-hover:scale-105';
                    img.onerror = () => { img.replaceWith(makePlaceholder()); };
                    cardElement.appendChild(img);
                } else {
                    cardElement.appendChild(makePlaceholder());
                }

                cardGrid.appendChild(cardElement);

                // 클릭 시 중앙에 크게 표시 (이미지가 있을 때만)
                cardElement.addEventListener('click', (e) => {
                    e.preventDefault();
                    if (image_url) openCardLightbox(card);
                });
            });
        }

        // 카드 개수 업데이트
        cardCountEl.textContent = `총 ${cardsToRender.length}장의 카드를 찾았습니다.`;
    }

    /**
     * 현재 필터 값에 따라 카드를 필터링하고 다시 렌더링하는 함수
     */
    function applyFilters() {
        // 1. 필터 값 가져오기
        const searchTerm = searchInput.value.toLowerCase();
        
        const selectedTypes = Array.from(typeCheckboxes)
            .filter(cb => cb.checked)
            .map(cb => cb.value);
        
        const selectedSchool = schoolSelect.value; // ''이면 전체 학원
        const selectedClub = clubSelect.value;     // ''이면 전체 동아리
        
        const minCost = parseInt(costMinInput.value, 10) || 0;
        const maxCost = parseInt(costMaxInput.value, 10) || Infinity;

        // 2. 필터링 수행
        const filteredCards = allCards.filter(card => {
            // 검색어 필터 (서버 데이터에는 카드 이름이 없어 tid 기준)
            const nameMatch = getCardTitle(card).toLowerCase().includes(searchTerm)
                || (card.tid || '').toLowerCase().includes(searchTerm);

            // 타입 필터
            const typeMatch = selectedTypes.length === 0 || selectedTypes.includes(card.type);

            // 학원/동아리 필터 (서버의 academy / club_titles 필드 기준)
            const schoolMatch = !selectedSchool || (card.academy || '') === selectedSchool;
            const clubMatch = !selectedClub || (card.club_titles || []).includes(selectedClub);

            // 코스트 필터 (게임 내 사용 코스트 = mana)
            const mana = card.mana || 0;
            const costMatch = mana >= minCost && mana <= maxCost;

            return nameMatch && typeMatch && schoolMatch && clubMatch && costMatch;
        });

        // 3. 다시 렌더링
        renderCards(filteredCards);
    }

    /**
     * 필터 초기화 함수
     */
    function resetFilters() {
        // 필터 폼 요소 초기화 (HTML form.reset()과 동일)
        searchInput.value = '';
        typeCheckboxes.forEach(cb => cb.checked = false);
        schoolSelect.value = '';
        clubSelect.value = '';
        costMinInput.value = '';
        costMaxInput.value = '';

        updateClubOptions(); // 동아리 드롭다운을 전체 목록으로 복원
        applyFilters();
    }

    /**
     * 카드 필터링 시스템 초기화
     */
    async function initCardFilter() {
        await fetchCardData();

        buildFilterOptions(); // 카드 데이터에서 학원/동아리 드롭다운 구성
        renderCards(allCards);

        // 폼 제출(검색창에서 Enter 등) 시 페이지가 새로고침되어
        // 메인 홈으로 돌아가는 문제 방지
        document.getElementById('card-filter-form').addEventListener('submit', (e) => {
            e.preventDefault();
        });

        // 필터 요소에 이벤트 리스너 바인딩
        searchInput.addEventListener('input', applyFilters);
        typeCheckboxes.forEach(cb => cb.addEventListener('change', applyFilters));
        schoolSelect.addEventListener('change', () => {
            updateClubOptions(); // 선택한 학원 소속 동아리만 표시
            applyFilters();
        });
        clubSelect.addEventListener('change', applyFilters);
        costMinInput.addEventListener('input', applyFilters);
        costMaxInput.addEventListener('input', applyFilters);
        resetButton.addEventListener('click', (e) => {
            e.preventDefault(); // 기본 form reset 방지
            resetFilters();
        });
    }

    // 카드 필터 시스템 실행
    initCardFilter();
});
