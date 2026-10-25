const UserModel = require('../../users/users.model');

module.exports = {

    id: "user-list",
    title: "유저 목록",
    description: "유저별 접속 시간, 플레이 수 등의 데이터",
    order: 1,

    columns: [
        { key: "username", label: "유저명", type: "text" },
        { key: "elo", label: "ELO", type: "number" },
        { key: "coins", label: "코인", type: "number" },
        { key: "matches", label: "플레이 수", type: "number" },
        { key: "victories", label: "승리", type: "number" },
        { key: "defeats", label: "패배", type: "number" },
        { key: "deck_count", label: "덱 수", type: "number" },
        { key: "last_login_time", label: "마지막 로그인", type: "date" },
        { key: "last_online_time", label: "마지막 접속", type: "date" },
        { key: "account_create_time", label: "가입일", type: "date" },
    ],

    getRows: async () => {
        const users = await UserModel.getAll();
        return users.map((user) => ({
            username: user.username,
            elo: user.elo,
            coins: user.coins,
            matches: user.matches,
            victories: user.victories,
            defeats: user.defeats,
            deck_count: (user.decks || []).length,
            last_login_time: user.last_login_time,
            last_online_time: user.last_online_time,
            account_create_time: user.account_create_time,
        }));
    },
};
