// clubs.json is a curated snapshot of the game's club assets, independent of /cards.
// media: null (preparing), { type: 'image', src: '/media/example.gif' }, or
// { type: 'video', src: '/media/example.mp4', poster: '/media/poster.jpg' }.
document.addEventListener('DOMContentLoaded', () => {
    const section = document.getElementById('page-clubs');
    if (!section) return;

    const schools = document.getElementById('club-schools');
    const status = document.getElementById('club-status');
    const title = document.getElementById('club-title');
    const academy = document.getElementById('club-academy');
    const cardArea = document.getElementById('club-card');
    const mediaArea = document.getElementById('club-media');
    const reducedMotion = window.matchMedia('(prefers-reduced-motion: reduce)');
    let selectedId = null;
    let video = null;

    function placeholder(container, heading, message) {
        container.replaceChildren();
        const box = document.createElement('div');
        box.className = 'club-placeholder';
        const label = document.createElement('strong');
        label.textContent = heading;
        const description = document.createElement('p');
        description.textContent = message;
        box.append(label, description);
        container.appendChild(box);
    }

    function clearVideo() {
        if (!video) return;
        video.pause();
        video.removeAttribute('src');
        video.load();
        video = null;
    }

    function playVideo() {
        if (video && section.classList.contains('page-active') && !reducedMotion.matches) {
            video.play().catch(() => {}); // Native controls remain available if autoplay is blocked.
        }
    }

    function selectClub(club, school) {
        if (selectedId === club.id) return;
        selectedId = club.id;
        schools.querySelectorAll('.club-choice').forEach(button => {
            button.setAttribute('aria-pressed', String(button.dataset.club === club.id));
        });
        title.textContent = club.title;
        academy.textContent = school.name;

        if (club.card) {
            const image = document.createElement('img');
            image.src = club.card;
            image.alt = `${club.title} 카드`;
            image.addEventListener('error', () => {
                placeholder(cardArea, '카드 이미지 준비 중', club.title);
            }, { once: true });
            cardArea.replaceChildren(image);
        } else {
            placeholder(cardArea, '카드 이미지 준비 중', club.title);
        }

        clearVideo();
        const media = club.media;
        if (media && media.src && (media.type === 'video' || media.type === 'image')) {
            const element = document.createElement(media.type === 'video' ? 'video' : 'img');
            if (media.type === 'video') {
                video = element;
                video.controls = true;
                video.loop = true;
                video.muted = true;
                video.playsInline = true;
                video.preload = 'metadata';
                video.setAttribute('aria-label', `${club.title} 대표 활용 예시`);
                if (media.poster) video.poster = media.poster;
            } else {
                element.alt = `${club.title} 대표 활용 예시`;
            }
            element.addEventListener('error', () => {
                clearVideo();
                placeholder(mediaArea, '영상을 불러오지 못했습니다', '다른 동아리를 선택하거나 잠시 후 다시 확인해 주세요.');
            }, { once: true });
            element.src = media.src;
            mediaArea.replaceChildren(element);
            playVideo();
        } else {
            placeholder(mediaArea, '대표 활용 영상 준비 중', '이 동아리의 플레이 예시가 이곳에 표시됩니다.');
        }
    }

    async function loadClubs() {
        status.textContent = '동아리를 불러오는 중입니다.';
        try {
            const response = await fetch('/clubs.json');
            if (!response.ok) throw new Error('Club catalog unavailable');
            const groups = await response.json();
            if (!Array.isArray(groups) || !groups.some(group => group.clubs?.length)) {
                throw new Error('Empty club catalog');
            }
            schools.replaceChildren();
            let initial = null;
            groups.forEach(school => {
                if (!school.clubs?.length) return;
                const group = document.createElement('section');
                group.className = 'club-school';
                const heading = document.createElement('h3');
                heading.id = `club-school-${school.id}`;
                heading.textContent = school.name;
                const row = document.createElement('div');
                row.className = 'club-icon-row';
                row.setAttribute('role', 'group');
                row.setAttribute('aria-labelledby', heading.id);
                school.clubs.forEach(club => {
                    const button = document.createElement('button');
                    button.type = 'button';
                    button.className = 'club-choice';
                    button.dataset.club = club.id;
                    button.setAttribute('aria-pressed', 'false');
                    button.setAttribute('aria-controls', 'club-detail');
                    const hex = document.createElement('span');
                    hex.className = 'club-hex';
                    const frame = document.createElement('img');
                    frame.src = '/images/clubs/frame.png';
                    frame.alt = '';
                    frame.className = 'club-hex-frame';
                    const icon = document.createElement('img');
                    icon.src = club.icon || '/images/clubs/default.png';
                    icon.alt = '';
                    icon.className = 'club-hex-icon';
                    icon.addEventListener('error', () => {
                        icon.src = '/images/clubs/default.png';
                    }, { once: true });
                    const label = document.createElement('span');
                    label.className = 'club-choice-label';
                    label.textContent = club.title;
                    hex.append(frame, icon);
                    button.append(hex, label);
                    button.addEventListener('click', () => selectClub(club, school));
                    row.appendChild(button);
                    if (!initial || club.id === 'Make_Up_Work_Club') initial = { club, school };
                });
                group.append(heading, row);
                schools.appendChild(group);
            });
            status.textContent = '';
            selectClub(initial.club, initial.school);
        } catch (error) {
            status.textContent = '동아리를 불러오지 못했습니다. ';
            const retry = document.createElement('button');
            retry.type = 'button';
            retry.className = 'club-retry';
            retry.textContent = '다시 시도';
            retry.addEventListener('click', loadClubs);
            status.appendChild(retry);
        }
    }

    document.addEventListener('site:page-change', () => {
        if (!section.classList.contains('page-active')) {
            if (video) video.pause();
        } else {
            playVideo();
        }
    });
    reducedMotion.addEventListener('change', () => {
        if (reducedMotion.matches && video) video.pause();
    });
    loadClubs();
});
