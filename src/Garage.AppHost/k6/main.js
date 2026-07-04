import http from "k6/http";
import { sleep } from "k6";

export default function () {
    http.get(`${__ENV.services__apiService__http__0}/lemans/winners`);

    sleep(1);
}
