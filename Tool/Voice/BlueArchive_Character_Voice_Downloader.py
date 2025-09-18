import os
import time
import requests
from selenium import webdriver
from selenium.webdriver.common.by import By
from selenium.webdriver.chrome.service import Service
from selenium.common.exceptions import NoSuchElementException, StaleElementReferenceException
from webdriver_manager.chrome import ChromeDriverManager
from urllib.parse import unquote
import re

# 다운로드 경로 설정 및 폴더 생성
DOWNLOAD_DIR = "downloads"
if not os.path.exists(DOWNLOAD_DIR):
    os.makedirs(DOWNLOAD_DIR)

# 웹 드라이버 설정 (Chrome)
print("웹 드라이버를 설정하는 중...")
service = Service(ChromeDriverManager().install())
driver = webdriver.Chrome(service=service)

# 기본 URL
base_url = "https://bluearchive.wiki/wiki/Category:Dialogs_by_character"
driver.get(base_url)
print("메인 페이지에 접속했습니다.")
time.sleep(5)

# 모든 캐릭터 카테고리 링크 찾기
try:
    character_links_xpath = '//div[@id="mw-subcategories"]//a'
    character_links = driver.find_elements(By.XPATH, character_links_xpath)
    print(f"총 {len(character_links)}개의 캐릭터 페이지 링크를 찾았습니다.")
    
    # 링크가 StaleElementReferenceException 오류를 일으킬 수 있으므로 URL만 리스트로 저장
    character_urls = [link.get_attribute('href') for link in character_links]

except NoSuchElementException:
    print("캐릭터 페이지 링크를 찾을 수 없습니다. 프로그램을 종료합니다.")
    driver.quit()
    exit()

# 각 캐릭터 페이지 순회
for character_url in character_urls:
    print(f"\n캐릭터 페이지로 이동: {character_url}")
    driver.get(character_url)
    time.sleep(5)

    try:
        # 현재 페이지의 모든 오디오 링크 항목 찾기
        audio_items_xpath = '//*[@id="mw-category-media"]/ul/li/div[2]/a'
        audio_items = driver.find_elements(By.XPATH, audio_items_xpath)
        
        current_page_url = driver.current_url

        print(f"  현재 페이지에서 {len(audio_items)}개의 오디오 링크를 찾았습니다.")

        for i in range(len(audio_items)):
            try:
                item = driver.find_elements(By.XPATH, audio_items_xpath)[i]
                audio_page_link = item.get_attribute('href')

                print(f"  > 파일 페이지 접속: {audio_page_link}")
                driver.get(audio_page_link)
                time.sleep(3)

                download_link_xpath = '//*[@id="mw-content-text"]/table/tbody/tr/td[3]/a'
                download_link = driver.find_element(By.XPATH, download_link_xpath).get_attribute('href')
                
                # 파일명 처리 및 폴더 경로 설정 (기존 로직 유지)
                raw_file_name = os.path.basename(download_link).split('?')[0]
                decoded_file_name = unquote(raw_file_name)
                
                base, ext = os.path.splitext(decoded_file_name)
                if base.endswith('.ogg'):
                    base = base[:-4]
                for char in ['<', '>', ':', '"', '/', '\\', '|', '?', '*']:
                    base = base.replace(char, '')

                match = re.search(r'(.+?)_\((.+?)\)', base)
                if match:
                    character_name = match.group(1)
                    costume_name = match.group(2)
                    sub_folder_name = f"{character_name}_{costume_name}"
                else:
                    character_name = base.split('_')[0]
                    sub_folder_name = character_name
                
                character_dir = os.path.join(DOWNLOAD_DIR, character_name)
                sub_dir = os.path.join(character_dir, sub_folder_name)
                os.makedirs(sub_dir, exist_ok=True)
                
                final_file_name = f"{base}{ext}"
                file_path = os.path.join(sub_dir, final_file_name)

                if os.path.exists(file_path):
                    print(f"  ✅ 파일이 이미 존재합니다. 건너뜀: {final_file_name}")
                else:
                    headers = {'User-Agent': 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/108.0.0.0 Safari/537.36'}
                    print(f"  📥 파일 다운로드 시작: {final_file_name}")
                    with requests.get(download_link, headers=headers, stream=True) as r:
                        r.raise_for_status()
                        with open(file_path, 'wb') as f:
                            for chunk in r.iter_content(chunk_size=8192):
                                f.write(chunk)
                    print(f"  🎉 파일 다운로드 완료: {final_file_name}")
                
                driver.get(current_page_url)
                time.sleep(3)

            except NoSuchElementException:
                print("  오디오 다운로드 링크를 찾을 수 없습니다. 다음 항목으로 넘어갑니다.")
                driver.get(current_page_url)
                time.sleep(3)
            except StaleElementReferenceException:
                print("  페이지 요소가 변경되어 재시도합니다.")
                driver.get(current_page_url)
                time.sleep(3)
                continue

    except Exception as e:
        print(f"예기치 않은 오류가 발생했습니다: {e}")
