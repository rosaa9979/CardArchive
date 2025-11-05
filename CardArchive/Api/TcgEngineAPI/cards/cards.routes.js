const CardController = require('./cards.controller');
const AuthTool = require('../authorization/auth.tool');
const config = require('../config');
const multer = require('multer');

const ADMIN = config.permissions.ADMIN; //Highest permision, can read and write all users
const SERVER = config.permissions.SERVER; //Higher permission, can read all users
const USER = config.permissions.USER; //Lowest permision, can only do things on same user

const upload = multer({ 
    storage: multer.memoryStorage(),
    limits: { 
        fileSize: 10 * 1024 * 1024  // 10MB 제한
    },
    fileFilter: (req, file, cb) => {
        if (file.mimetype === 'image/png' || file.mimetype === 'image/jpeg') {
            cb(null, true);
        } else {
            cb(new Error('Only PNG/JPEG images are allowed'), false);
        }
    }
});

exports.route = function (app) {

    app.get('/cards/:tid', [
        CardController.GetCard
    ]);

    app.get('/cards', [
        CardController.GetAll
    ]);

    app.post('/cards/add', [
        AuthTool.isValidJWT,
        AuthTool.isPermissionLevel(ADMIN),
        upload.fields([                      
            { name: 'card_data', maxCount: 1 },
            { name: 'image_file', maxCount: 1 }
        ]),
        CardController.AddCard
    ]);

    app.delete("/cards/:tid", [
        AuthTool.isValidJWT,
        AuthTool.isPermissionLevel(ADMIN),
        CardController.DeleteCard
    ]);

    app.delete("/cards", [
        AuthTool.isValidJWT,
        AuthTool.isPermissionLevel(ADMIN),
        CardController.DeleteAll
    ]);
};