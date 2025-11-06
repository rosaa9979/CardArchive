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

        // '게임 소개' 페이지로 전환 시, 첫 번째 슬라이드를 활성화
        if (pageId === 'page-info') {
            // 모든 슬라이드 비활성화 후 첫 번째 슬라이드 활성화
            document.querySelectorAll('.scroll-slide').forEach(slide => slide.classList.remove('slide-active'));
            const firstSlide = document.getElementById('info-slide-0');
            if (firstSlide) {
                firstSlide.classList.add('slide-active');
            }
        }
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

    // --- 3. '게임 소개' 페이지 스크롤 로직 (Intersection Observer) ---
    function initInfoPageScroll() {
        const slides = document.querySelectorAll('.scroll-slide');
        const triggers = document.querySelectorAll('.scroll-trigger');

        if (triggers.length === 0) return; // '게임 소개' 페이지 요소가 없으면 실행 안함

        const observerOptions = {
            root: null, // 뷰포트를 root로 사용
            rootMargin: '0px',
            threshold: 0.5 // 트리거가 50% 이상 보일 때 감지
        };

        const handleIntersect = (entries) => {
            entries.forEach(entry => {
                if (entry.isIntersecting) {
                    const slideId = entry.target.dataset.slide;
                    
                    // 모든 슬라이드 비활성화
                    slides.forEach(slide => slide.classList.remove('slide-active'));
                    
                    // 해당 슬라이드 활성화
                    const targetSlide = document.getElementById(slideId);
                    if (targetSlide) {
                        targetSlide.classList.add('slide-active');
                    }
                }
            });
        };

        const observer = new IntersectionObserver(handleIntersect, observerOptions);
        triggers.forEach(trigger => observer.observe(trigger));
    }

    initInfoPageScroll(); // 스크롤 로직 실행

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
                const cardElement = document.createElement('a');
                cardElement.href = '#'; // TBD: 카드 상세 페이지 링크?
                cardElement.className = 'group';
                cardElement.setAttribute('title', card.name);

                const image_url = card.image.startsWith('/public') ? card.image.replace('/public', '') : card.image;
                
                cardElement.innerHTML = `
                    <img src="${image_url}" alt="${card.name}" class="card-image w-full rounded-lg shadow-sm transition-all group-hover:shadow-xl group-hover:scale-105">
                `;
                cardGrid.appendChild(cardElement);
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
        
        const selectedSchool = schoolSelect.value === '전체 학원' ? '' : schoolSelect.value;
        const selectedClub = clubSelect.value === '전체 동아리' ? '' : clubSelect.value;
        
        const minCost = parseInt(costMinInput.value, 10) || 0;
        const maxCost = parseInt(costMaxInput.value, 10) || Infinity;

        // 2. 필터링 수행
        const filteredCards = allCards.filter(card => {
            // 검색어 필터
            const nameMatch = card.name.toLowerCase().includes(searchTerm);
            
            // 타입 필터
            const typeMatch = selectedTypes.length === 0 || selectedTypes.includes(card.type);
            
            // 학원 필터
            const schoolMatch = !selectedSchool || card.school === selectedSchool;
            
            // 동아리 필터
            const clubMatch = !selectedClub || card.club === selectedClub;

            // 코스트 필터
            const costMatch = card.cost >= minCost && card.cost <= maxCost;
            
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

        applyFilters();
    }

    /**
     * 카드 필터링 시스템 초기화
     */
    async function initCardFilter() {
        // Mock 데이터 사용
        await fetchCardData();

        renderCards(allCards);
        
        // 필터 요소에 이벤트 리스너 바인딩
        searchInput.addEventListener('input', applyFilters);
        typeCheckboxes.forEach(cb => cb.addEventListener('change', applyFilters));
        schoolSelect.addEventListener('change', applyFilters);
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
