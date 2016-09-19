# Google Image Search Scraper that searches for known WebCam URL patterns (works in August of 2016)
# by awgh@awgh.org

# This product includes GeoLite2 data created by MaxMind, available from <a href="http://www.maxmind.com">http://www.maxmind.com</a>.

import urllib2
import ssl
from bs4 import BeautifulSoup
import json
import os
import geoip2.database
import linecache
import sys
import xml.etree.cElementTree as ET
from urlparse import urlparse

root = ET.Element("urls")

ctx = ssl.create_default_context()
ctx.check_hostname = False
ctx.verify_mode = ssl.CERT_NONE

def PrintException():
    exc_type, exc_obj, tb = sys.exc_info()
    f = tb.tb_frame
    lineno = tb.tb_lineno
    filename = f.f_code.co_filename
    linecache.checkcache(filename)
    line = linecache.getline(filename, lineno, f.f_globals)
    print 'EXCEPTION IN ({}, LINE {} "{}"): {}'.format(filename, lineno, line.strip(), exc_obj)

geoip = geoip2.database.Reader('GeoLite2-City.mmdb')

def get_soup(url,header):
    content = urllib2.urlopen(urllib2.Request(url,headers=header)).read()
    #print content
    return BeautifulSoup(content, 'html.parser')

def googleImageSearch(query, DIR = None, TAG = None):
    url="https://www.google.com/search?q="+query+"&source=lnms&tbm=isch"
    print url

    header={'User-Agent':"Mozilla/5.0 (Windows NT 6.1; WOW64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/43.0.2357.134 Safari/537.36"}
    soup = get_soup(url,header)

    ActualImages=[]# contains the link for Large original images, type of  image
    for a in soup.find_all("div",{"class":"rg_meta"}):
        link , Type =json.loads(a.text)["ou"]  ,json.loads(a.text)["ity"]
        print link +':::'+Type
        ActualImages.append((link,Type))
    print  "there are total" , len(ActualImages),"images"

    if(DIR != None and TAG != None): # save images if DIR and TAG are set
        if not os.path.exists(DIR):
            os.mkdir(DIR)
        DIR = os.path.join(DIR, TAG) 
        if not os.path.exists(DIR):
            os.mkdir(DIR)
            
    for i , (img , Type) in enumerate( ActualImages):
        try:
            pr = urlparse(img)                
            q = query
            if query.startswith('inurl'):
                q = query[7:-1]
            if not q.lower().startswith(pr[2].lower()):
                print 'MISMATCH:'
                print q +' -> '+pr[2]
                continue    
            
            req = urllib2.Request(img, headers={'User-Agent' : header})
            resp = urllib2.urlopen(req, timeout=10.0, context=ctx)
            mime = resp.headers['Content-Type']
            peer = resp.fp._sock.fp._sock.getpeername()
        
            imageMime = False
            if mime.startswith('image/') or mime == 'application/octet-stream':
                imageMime = True
        
            if(DIR != None and TAG != None and imageMime):
                raw_img = resp.read()
                cntr = len([i for i in os.listdir(DIR) if TAG in i]) + 1
                if len(Type)==0:
                    f = open(os.path.join(DIR , TAG + "_"+ str(cntr)+".jpg"), 'wb')
                else :
                    f = open(os.path.join(DIR , TAG + "_"+ str(cntr)+"."+Type), 'wb')
                f.write(raw_img)
                f.close()
                
            # if we make it to here, save link
            g = geoip.city(peer[0])

            et = ET.SubElement(root, "url")
            et.attrib['link'] = img
            if mime:
                et.attrib['mime'] = mime
            if peer:
                et.attrib['ip'] = peer[0]
                et.attrib['port'] = str(int(peer[1]))
            if g:
                if g.city and g.city.name:
                    et.attrib['city'] = g.city.name
                if g.subdivisions and g.subdivisions.most_specific and g.subdivisions.most_specific.name:
                    et.attrib['region'] = g.subdivisions.most_specific.name
                if g.country and g.country.name:
                    et.attrib['country'] = g.country.name
                if g.location:
                    if g.location.latitude:
                        et.attrib['lat'] = str(float(g.location.latitude))
                    if g.location.longitude:
                        et.attrib['long'] = str(float(g.location.longitude))

        except Exception as e:
            PrintException()

def googleImageInURLSearch(query, DIR = None, TAG = None): 
    googleImageSearch("inurl:\""+query+"\"", DIR, TAG)
            
googleImageInURLSearch("/jpg/image.jpg?r=", "Pictures", "Axis_JPEG")
googleImageInURLSearch("/mjpg/video.mjpg", "Pictures", "Axis_MJPEG")

googleImageInURLSearch("/record/current.jpg", "Pictures", "Mobotix_JPEG")
googleImageInURLSearch("/cgi-bin/faststream.jpg?stream=", "Pictures", "Mobotix_MJPEG")

googleImageInURLSearch("/oneshotimage.jpg", "Pictures", "SNC")

googleImageInURLSearch("/SnapshotJPEG?Resolution=", "Pictures", "Panasonic_JPEG")
googleImageInURLSearch("/nphMotionJpeg?Resolution=", "Pictures", "Panasonic_MJPEG")

tree = ET.ElementTree(root)
tree.write("urls.xml")
geoip.close()

print "Completed"
