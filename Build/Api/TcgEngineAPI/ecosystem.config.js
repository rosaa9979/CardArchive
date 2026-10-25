module.exports = {
  apps: [{
    name: 'tcgapi',
    script: 'server.js',
    instances: 1,                    // 메모리 누수 디버깅중엔 1개만
    
    // 메모리 관리
    max_memory_restart: '150M',      // 150MB 초과시 재시작
    min_uptime: '10s',              // 최소 10초 실행
    max_restarts: 10,               // 최대 재시작 횟수
    
    // Node.js 최적화
    node_args: '--expose-gc --max-old-space-size=200',
    
    // 로깅 강화
    log_date_format: 'YYYY-MM-DD HH:mm:ss Z',
    merge_logs: true,
    time: true,
    
    // 환경 설정
    env: {
      NODE_ENV: 'production',
      TZ: 'Asia/Seoul'
    },
    
    // 에러 처리
    autorestart: true,
    watch: false,
    
    // 성능 모니터링
    pmx: true,
  }]
}